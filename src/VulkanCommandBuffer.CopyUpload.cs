using Silk.NET.Vulkan;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;
using System.Runtime.CompilerServices;

namespace SilkVulkanModule;

internal unsafe partial class VulkanCommandBuffer
{
    const string OOBSourceArrayError = "Provided out of bounds borders for source array!";

    const string OOBSourceBufferError = "Provided out of bounds borders for source buffer!";
    const string OOBDestinationBufferError = "Provided out of bounds borders for destination buffer!";

    const string OOBDestinationTextureError = "Provided out of bounds borders for destination texture!";

    const string OversizeError = "Source size is bigger than destination size!";

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
            throw new InvalidOperationException(RecordingError);
        }

        if (src.Size < srcByteSize + srcByteOffset)
        {
            throw new ArgumentException(OOBSourceBufferError);
        }

        if (dst.Size < dstByteSize + dstByteOffset)
        {
            throw new ArgumentException(OOBDestinationBufferError);
        }

        if (srcByteSize > dstByteSize)
        {
            throw new ArgumentException(OversizeError);
        }

        if (src is not VulkanDeviceBuffer vkSrcBuffer)
        {
            throw new ArgumentException(DifferentBackendError, nameof(src));
        }

        if (dst is not VulkanDeviceBuffer vkDstBuffer)
        {
            throw new ArgumentException(DifferentBackendError, nameof(dst));
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
            throw new ArgumentException(OOBDestinationBufferError);
        }

        if (src.Length * stride < srcByteSize + srcByteOffset)
        {
            throw new ArgumentException(OOBSourceArrayError);
        }

        if (srcByteSize > dstByteSize)
        {
            throw new ArgumentException(OversizeError);
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
