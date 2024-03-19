using Silk.NET.Vulkan;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;
using System.Runtime.CompilerServices;

namespace SilkVulkanModule;

internal unsafe partial class VulkanCommandBuffer
{
    public override void Copy(Texture dst, uint dstX, uint dstY, uint dstWidth, uint dstHeight,
        Texture src, uint srcX, uint srcY, uint srcWidth, uint srcHeight)
    {
        throw new NotImplementedException();
    }

    public override void Copy(DeviceBuffer dst, uint dstByteSize, uint dstByteOffset,
        DeviceBuffer src, uint srcByteSize, uint srcByteOffset)
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (src.Size < srcByteSize + srcByteOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for source buffer!");
        }

        if (dst.Size < dstByteSize + dstByteOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for destination buffer!");
        }

        if (srcByteSize > dstByteSize)
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

        var bufferCopy = new BufferCopy(srcByteOffset, dstByteOffset, srcByteSize);

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

    public override void Copy(DeviceBuffer dst, Texture src)
    {
        throw new NotImplementedException();
    }

    public override void Copy(Texture dst, uint dstX, uint dstY, uint dstWidth, uint dstHeight,
        DeviceBuffer src, uint srcRowLength, uint srcHeight, uint srcByteOffset)
    {
        throw new NotImplementedException();
    }

    public override void Copy(Texture dst, DeviceBuffer src)
    {
        throw new NotImplementedException();
    }

    public override void Copy(DeviceBuffer dst, uint dstRowLength, uint dstHeight, uint dstByteOffset,
        Texture src, uint srcX, uint srcY, uint srcWidth, uint srcHeight)
    {
        throw new NotImplementedException();
    }

    public override void Upload<T>(Texture dst, T[] src)
    {
        throw new NotImplementedException();
    }

    public override void Upload<T>(Texture dst, uint dstX, uint dstY, uint dstWidth, uint dstHeight,
        T[] src, uint srcRowLength, uint srcHeight, uint srcByteOffset)
    {
        throw new NotImplementedException();
    }

    public override void Upload<T>(DeviceBuffer dst, uint dstByteSize, uint dstByteOffset,
        T[] src, uint srcByteSize, uint srcByteOffset)
    {
        var stride = sizeof(T);

        if (dst.Size < dstByteSize + dstByteOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for destination buffer!");
        }

        if (src.Length * stride < srcByteSize + srcByteOffset)
        {
            throw new ArgumentException($"Provided out of bounds borders for source array!");
        }

        if (srcByteSize > dstByteSize)
        {
            throw new ArgumentException("Source size is bigger than destination size!");
        }

        var buffer = _factory.CreateDeviceBuffer(unchecked((int)srcByteSize), BufferUsageType.Staging);
        var memory = buffer.MapToHost(BufferMapType.TwoWay, srcByteSize, dstByteOffset);

        fixed (T* pSrc = src)
        {
            Unsafe.CopyBlock(memory.Data.ToPointer(), pSrc, srcByteSize);
        }

        buffer.UnMap(memory);
        
        Copy(buffer, srcByteSize, srcByteOffset, dst, dstByteSize, dstByteOffset);
        
        DisposeQueue.Add(buffer);
    }
}
