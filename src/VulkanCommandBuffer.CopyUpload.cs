using Silk.NET.Vulkan;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;
using System.Runtime.CompilerServices;

namespace VulkanModule;

internal unsafe partial class VulkanCommandBuffer
{
    public override void Copy(Texture src, Texture dst)
    {
        throw new NotImplementedException();
    }

    public override void Copy(Texture src, uint srcSize, uint srcOffset, Texture dst, uint dstSize, uint dstOffset)
    {
        throw new NotImplementedException();
    }

    public override void Copy(DeviceBuffer src, uint srcSize, uint srcOffset, DeviceBuffer dst, uint dstSize, uint dstOffset)
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (src.Size < srcSize + srcOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for source buffer!");
        }

        if (dst.Size < dstSize + dstOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for destination buffer!");
        }

        if (srcSize > dstSize)
        {
            throw new ArgumentException("Source size is bigger than destination size!");
        }

        if (src is not VulkanDeviceBuffer vkSrcBuffer)
        {
            throw new ArgumentException("Source device buffer belongs to different backend!");
        }

        if (dst is not VulkanDeviceBuffer vkDstBuffer)
        {
            throw new ArgumentException("Destination device buffer belongs to different backend!");
        }

        var bufferCopy = new BufferCopy(srcOffset, dstOffset, srcSize);

        _vk.CmdCopyBuffer(CommandBuffer, vkSrcBuffer.Buffer, vkDstBuffer.Buffer, 1, in bufferCopy);

        AccessFlags dstAccess;
        if (vkDstBuffer.Usage == BufferUsageType.Vertex)
        {
            dstAccess = AccessFlags.VertexAttributeReadBit;
        }
        else
        {
            dstAccess = AccessFlags.None;
        }

        var barrier = new BufferMemoryBarrier()
        {
            SType = StructureType.BufferMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = dstAccess,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Buffer = vkDstBuffer.Buffer,
            Offset = bufferCopy.DstOffset,
            Size = bufferCopy.Size
        };

        PipelineStageFlags dstStage;
        if (vkDstBuffer.Usage == BufferUsageType.Vertex)
        {
            dstStage = PipelineStageFlags.VertexInputBit;
        }
        else
        {
            dstStage = PipelineStageFlags.AllCommandsBit;
        }

        _vk.CmdPipelineBarrier(CommandBuffer,
            PipelineStageFlags.TransferBit, dstStage,
            DependencyFlags.None,
            0, null,
            1, &barrier,
            0, null);
    }

    public override void Copy(DeviceBuffer src, Texture dst)
    {
        throw new NotImplementedException();
    }

    public override void Copy(DeviceBuffer src, uint srcSize, uint srcOffset, Texture dst, uint dstSize, uint dstOffset)
    {
        throw new NotImplementedException();
    }

    public override void Copy(Texture src, DeviceBuffer dst)
    {
        throw new NotImplementedException();
    }

    public override void Copy(Texture src, uint srcSize, uint srcOffset, DeviceBuffer dst, uint dstSize, uint dstOffset)
    {
        throw new NotImplementedException();
    }

    public override void Upload<T>(Texture dst, T[] src)
    {
        throw new NotImplementedException();
    }

    public override void Upload<T>(Texture dst, uint dstSize, uint dstOffset, T[] src, uint srcSize, uint srcOffset)
    {
        throw new NotImplementedException();
    }

    public override unsafe void Upload<T>(DeviceBuffer dst, uint dstSize, uint dstOffset, T[] src, uint srcSize, uint srcOffset)
    {
        var stride = sizeof(T);

        if (dst.Size < dstSize + dstOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for destination buffer!");
        }

        if (src.Length * stride < srcSize + srcOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for source array!");
        }

        if (srcSize > dstSize)
        {
            throw new ArgumentException("Source size is bigger than destination size!");
        }

        var buffer = _factory.CreateDeviceBuffer(unchecked((int)srcSize), BufferUsageType.Staging);
        var memory = buffer.MapToHost(BufferMapType.TwoWay, srcSize, dstOffset);

        unsafe
        {
            fixed (T* pSrc = src)
            {
                Unsafe.CopyBlock(memory.Data.ToPointer(), pSrc, srcSize);
            }
        }

        buffer.UnMap(memory);
        DisposeQueue.Add(buffer);
    }
}
