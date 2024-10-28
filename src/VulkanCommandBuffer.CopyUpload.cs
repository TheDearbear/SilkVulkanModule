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

    const string OOBSourceTextureError = "Provided out of bounds borders for source texture!";
    const string OOBDestinationTextureError = "Provided out of bounds borders for destination texture!";

    const string OversizeError = "Source size is bigger than destination size!";

    public override void Copy(Texture dst, uint dstX, uint dstY, uint dstWidth, uint dstHeight,
        Texture src, uint srcX, uint srcY, uint srcWidth, uint srcHeight)
    {
        if (!Recording)
        {
            throw new InvalidOperationException(RecordingError);
        }

        if (dst.Width < dstWidth + dstX || dst.Height < dstHeight + dstY)
        {
            throw new ArgumentException(OOBDestinationTextureError);
        }

        if (src.Width < srcWidth + srcX || src.Height < srcHeight + srcY)
        {
            throw new ArgumentException(OOBSourceTextureError);
        }

        if (srcWidth > dstWidth || srcHeight > dstHeight)
        {
            throw new ArgumentException(OversizeError);
        }

        if (src is not VulkanTexture srcTexture)
        {
            throw new ArgumentException(DifferentBackendError, nameof(src));
        }

        if (!srcTexture.Image.HasValue)
        {
            throw new ArgumentException(TextureNotLoadedError, nameof(src));
        }

        if (dst is not VulkanTexture dstTexture)
        {
            throw new ArgumentException(DifferentBackendError, nameof(dst));
        }

        var dstImage = dstTexture.CreateImage();

        var region = new ImageCopy()
        {
            SrcOffset = new(unchecked((int)srcX), unchecked((int)srcY)),
            SrcSubresource = new(ImageAspectFlags.None, 0, 0, 1),
            DstOffset = new(unchecked((int)dstX), unchecked((int)dstY)),
            DstSubresource = new(ImageAspectFlags.None, 0, 0, 1),
            Extent = new(srcWidth, srcHeight)
        };

        _vk.CmdCopyImage(CommandBuffer, srcTexture.Image.Value.Item1, srcTexture.NativeLayout,
            dstImage.Item1, dstTexture.NativeLayout, 1, in region);

        var access = AccessFlags.None;
        if (dstTexture.Usage == TextureUsage.Sampled)
        {
            access = AccessFlags.ShaderReadBit;
        }
        else if (dstTexture.Usage == TextureUsage.DepthStencilAttachment)
        {
            access = AccessFlags.DepthStencilAttachmentReadBit;
        }
        else if (dstTexture.Usage == TextureUsage.Storage)
        {
            access = AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit;
        }
        else if (dstTexture.Usage == TextureUsage.ColorAttachment)
        {
            access = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit;
        }
        else if (dstTexture.Usage == TextureUsage.InputAttachment)
        {
            access = AccessFlags.InputAttachmentReadBit;
        }

        var barrier = new ImageMemoryBarrier()
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.TransferWriteBit,
            DstAccessMask = access,
            OldLayout = dstTexture.NativeLayout,
            NewLayout = dstTexture.NativeLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = dstImage.Item1,
            SubresourceRange = new(ImageAspectFlags.None, 0, 1, 0, 1)
        };

        _vk.CmdPipelineBarrier(CommandBuffer,
            PipelineStageFlags.TransferBit, PipelineStageFlags.AllCommandsBit,
            DependencyFlags.None,
            0, null,
            0, null,
            1, &barrier);
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

    public override void Copy(Texture dst, uint dstX, uint dstY, uint dstWidth, uint dstHeight,
        DeviceBuffer src, uint srcRowLength, uint srcHeight, uint srcByteOffset)
    {
        throw new NotImplementedException();
    }

    public override void Copy(DeviceBuffer dst, uint dstRowLength, uint dstHeight, uint dstByteOffset,
        Texture src, uint srcX, uint srcY, uint srcWidth, uint srcHeight)
    {
        throw new NotImplementedException();
    }

    public override void Upload<T>(Texture dst, uint dstX, uint dstY, uint dstWidth, uint dstHeight,
        T[] src, uint srcRowLength, uint srcHeight, uint srcByteOffset)
    {
        if (!Recording)
        {
            throw new InvalidOperationException(RecordingError);
        }

        if (dst.Width < dstWidth + dstX || dst.Height < dstHeight + dstY)
        {
            throw new ArgumentException(OOBDestinationTextureError);
        }

        var stride = sizeof(T);

        if (src.Length * stride < srcRowLength * srcHeight * stride + srcByteOffset)
        {
            throw new ArgumentException(OOBSourceArrayError);
        }

        uint byteSize = srcRowLength * srcHeight * unchecked((uint)stride);

        var buffer = _factory.CreateDeviceBuffer(unchecked((int)byteSize), BufferUsageType.Staging);
        var memory = buffer.MapToHost(BufferMapType.TwoWay, byteSize, 0);

        fixed (T* pSrc = src)
        {
            Unsafe.CopyBlock(memory.Data.ToPointer(), (byte*)pSrc + srcByteOffset, byteSize);
        }

        buffer.UnMap(memory);

        Copy(dst, dstX, dstY, dstWidth, dstHeight,
            buffer, srcRowLength, srcHeight, 0);

        DisposeQueue.Add(buffer);
    }

    public override void Upload<T>(DeviceBuffer dst, uint dstByteSize, uint dstByteOffset,
        T[] src, uint srcByteSize, uint srcByteOffset)
    {
        if (!Recording)
        {
            throw new InvalidOperationException(RecordingError);
        }

        if (dst.Size < dstByteSize + dstByteOffset)
        {
            throw new ArgumentException(OOBDestinationBufferError);
        }

        var stride = sizeof(T);

        if (src.Length * stride < srcByteSize + srcByteOffset)
        {
            throw new ArgumentException(OOBSourceArrayError);
        }

        if (srcByteSize > dstByteSize)
        {
            throw new ArgumentException(OversizeError);
        }

        var buffer = _factory.CreateDeviceBuffer(unchecked((int)srcByteSize), BufferUsageType.Staging);
        var memory = buffer.MapToHost(BufferMapType.TwoWay, srcByteSize, 0);

        fixed (T* pSrc = src)
        {
            Unsafe.CopyBlock(memory.Data.ToPointer(), (byte*)pSrc + srcByteOffset, srcByteSize);
        }

        buffer.UnMap(memory);
        
        Copy(dst, dstByteSize, dstByteOffset,
            buffer, srcByteSize, 0);
        
        DisposeQueue.Add(buffer);
    }
}
