using Silk.NET.Vulkan;
using Speed.Viewer.Render.Backend;
using System;

using VkBuffer = Silk.NET.Vulkan.Buffer;

namespace VulkanModule;

internal unsafe sealed class VulkanDeviceBuffer : DeviceBuffer
{
    public override bool CanBeMapped { get; }

    internal VkBuffer Buffer { get; }
    internal DeviceMemory DeviceMemory { get; }

    readonly uint _realSize;
    readonly PhysicalDevice _physicalDevice;
    readonly Device _device;
    readonly Vk _vk;
    
    public VulkanDeviceBuffer(Vk vk, PhysicalDevice physicalDevice, int size, BufferUsageType usage)
        : base(size, usage)
    {
        CanBeMapped = Usage.HasFlag(BufferUsageType.Staging);

        _vk = vk;
        _physicalDevice = physicalDevice;
        _device = _vk.CurrentDevice!.Value;

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = unchecked((ulong)size),
            Usage = MapUsage(usage)
        };

        VulkanTools.Ensure(_vk.CreateBuffer(_device, in createInfo, null, out var buffer));
        Buffer = buffer;

        _vk.GetBufferMemoryRequirements(_device, Buffer, out var memRequirements);

        _realSize = (uint)Math.Max(createInfo.Size, memRequirements.Size);

        MemoryPropertyFlags memFlags = usage.HasFlag(BufferUsageType.Staging) ?
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit :
            MemoryPropertyFlags.DeviceLocalBit;

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = _realSize,
            MemoryTypeIndex = GetVulkanMemoryType(memRequirements.MemoryTypeBits, memFlags)
        };

        VulkanTools.Ensure(_vk.AllocateMemory(_device, in allocInfo, null, out var memory));
        DeviceMemory = memory;
        VulkanTools.Ensure(_vk.BindBufferMemory(_device, Buffer, DeviceMemory, 0));
    }

    public override void Dispose()
    {
        _vk.DestroyBuffer(_device, Buffer, null);
        _vk.FreeMemory(_device, DeviceMemory, null);
    }

    public override MappedDeviceMemory MapToHost(BufferMapType type, uint size, uint offset)
    {
        if (!Usage.HasFlag(BufferUsageType.Staging))
        {
            throw new InvalidOperationException("Cannot map buffer without staging flag!");
        }

        if (size + offset > Size)
        {
            throw new ArgumentException("Provided out of bounds borders for buffer!");
        }

        void* pData = default;
        VulkanTools.Ensure(_vk.MapMemory(_device, DeviceMemory, offset, size, 0, &pData));
        
        MappedDeviceMemory mapped = new(size, offset, new(pData));
        MappedSection = mapped;
        return mapped;
    }

    public override void UnMap(MappedDeviceMemory mapped)
    {
        if (!Usage.HasFlag(BufferUsageType.Staging))
        {
            throw new InvalidOperationException("Cannot un-map buffer without staging flag!");
        }

        _vk.UnmapMemory(_device, DeviceMemory);
        MappedSection = null;
    }

    public bool BarrierBuffer(VulkanCommandBuffer cmd)
    {
        if (!cmd.Recording)
        {
            return false;
        }

        var bufferBarrier = new BufferMemoryBarrier()
        {
            SType = StructureType.BufferMemoryBarrier,
            SrcAccessMask = VulkanTools.GetAccessFlags(Usage),
            DstAccessMask = VulkanTools.GetAccessFlags(Usage),
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = Buffer,
            Offset = 0,
            Size = _realSize
        };

        _vk.CmdPipelineBarrier(cmd.CommandBuffer,
            PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TopOfPipeBit,
            DependencyFlags.ByRegionBit,
            0, null,
            1, in bufferBarrier,
            0, null);

        return true;
    }

    uint GetVulkanMemoryType(uint bits, MemoryPropertyFlags flags)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProps);

        return EnumMemoryTypes(bits, flags | MemoryPropertyFlags.HostCachedBit, memProps) ??
            EnumMemoryTypes(bits, flags, memProps) ??
            throw new ArgumentException("Unsupported memory properties!", nameof(flags));
    }

    static uint? EnumMemoryTypes(uint bits, MemoryPropertyFlags flags, PhysicalDeviceMemoryProperties memProps)
    {
        for (int i = 0; i < memProps.MemoryTypeCount || i < Vk.MaxMemoryTypes; i++)
        {
            if ((bits & (1 << i)) != 0 && memProps.MemoryTypes[i].PropertyFlags.HasFlag(flags))
            {
                return unchecked((uint)i);
            }
        }

        return null;
    }

    static BufferUsageFlags MapUsage(BufferUsageType usage)
    {
        BufferUsageFlags flags = 0;

        static void MapEnum(ref BufferUsageFlags output, BufferUsageType input, BufferUsageFlags mapTo, BufferUsageType mapFrom)
        {
            if (input.HasFlag(mapFrom))
            {
                output |= mapTo;
            }
        }

        MapEnum(ref flags, usage, BufferUsageFlags.UniformBufferBit, BufferUsageType.Uniform);
        MapEnum(ref flags, usage, BufferUsageFlags.StorageBufferBit, BufferUsageType.Storage);
        MapEnum(ref flags, usage, BufferUsageFlags.VertexBufferBit,  BufferUsageType.Vertex);
        MapEnum(ref flags, usage, BufferUsageFlags.IndexBufferBit,   BufferUsageType.Index);

        flags |= BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit;

        return flags;
    }
}
