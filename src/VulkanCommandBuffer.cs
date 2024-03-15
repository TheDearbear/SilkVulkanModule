using Silk.NET.Vulkan;
using Speed.Engine.Sceneries;
using Speed.Viewer.Render.Backend;
using Speed.Viewer.Render.Backend.Pipelines;
using System;
using System.Collections.Generic;
using System.Numerics;

using SpeedCommandBuffer = Speed.Viewer.Render.Backend.CommandBuffer;
using SpeedFramebuffer = Speed.Viewer.Render.Backend.Framebuffer;
using SpeedPipeline = Speed.Viewer.Render.Backend.Pipeline;
using SpeedIndexType = Speed.Viewer.Render.Backend.IndexType;

using VkBuffer = Silk.NET.Vulkan.Buffer;
using VkCommandBuffer = Silk.NET.Vulkan.CommandBuffer;
using VkIndexType = Silk.NET.Vulkan.IndexType;

namespace SilkVulkanModule;

internal unsafe sealed partial class VulkanCommandBuffer : SpeedCommandBuffer
{
    public override SpeedFramebuffer? CurrentFramebuffer { get; set; }

    SpeedPipeline? _pipeline;
    public override SpeedPipeline? CurrentPipeline
    {
        get => _pipeline;
        set
        {
            _pipeline = value;
            UpdatePipeline();
        }
    }

    ValueTuple<int, int>? _renderSize;
    public override ValueTuple<int, int>? RenderSize {
        get => _renderSize;
        set
        {
            _renderSize = value;
            UpdateRenderSize();
        }
    }

    internal VkCommandBuffer CommandBuffer { get; }
    internal VulkanSemaphore Semaphore { get; }

    internal List<DeviceBuffer> DisposeQueue { get; } = new(5);

    readonly IBackendFactory _factory;
    readonly CommandPool _pool;
    readonly Device _device;
    readonly Vk _vk;

    /// <summary>
    /// Creates command buffer from existing handler
    /// </summary>
    /// <param name="pool">Command pool for existing handled</param>
    /// <param name="handler">Existing handler</param>
    /// <remarks>
    /// Provided handler will not be synced and rather will be reset to initial state and also disposed in the end of object's lifecycle
    /// </remarks>
    public VulkanCommandBuffer(IBackendFactory factory, Vk vk, CommandPool pool, VkCommandBuffer handler)
    {
        _vk = vk;
        _factory = factory;
        _device = _vk.CurrentDevice!.Value;
        _pool = pool;
        CommandBuffer = handler;
        Semaphore = new(_vk);
    }

    public override bool Begin()
    {
        if (Recording)
        {
            return false;
        }

        var beginInfo = new CommandBufferBeginInfo()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        VulkanTools.Ensure(_vk.BeginCommandBuffer(CommandBuffer, in beginInfo));

        return Recording = true;
    }

    public override bool End()
    {
        if (!Recording)
        {
            return false;
        }

        VulkanTools.Ensure(_vk.EndCommandBuffer(CommandBuffer));
        RenderSize = null;
        Recording = false;
        return true;
    }

    public override void Dispose()
    {
        foreach (var disposable in DisposeQueue)
        {
            disposable.Dispose();
        }

        var buffer = CommandBuffer;
        _vk.FreeCommandBuffers(_device, _pool, 1, in buffer);
        Semaphore.Dispose();
    }

    public override void ClearDepthStencil(int index, float depth, uint stencil)
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (CurrentFramebuffer is null)
        {
            throw new InvalidOperationException("Current framebuffer is null!");
        }

        if (index >= CurrentFramebuffer.Attachments.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Provided index is out of range!");
        }

        if (CurrentFramebuffer.Attachments[index] is not VulkanTexture vkTexture)
        {
            throw new InvalidOperationException("Texture of framebuffer belongs to different backend!");
        }

        if (!vkTexture.Image.HasValue)
        {
            throw new InvalidOperationException("Texture of framebuffer is not loaded!");
        }

        var clearValues = new ClearDepthStencilValue(depth, stencil);

        var range = new ImageSubresourceRange(
            ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit,
            0, Vk.RemainingMipLevels,
            0, Vk.RemainingArrayLayers);

        if (vkTexture.Layout is not TextureLayout.General or TextureLayout.TransferDestination)
        {
            vkTexture.TransitionLayout(this, TextureLayout.TransferDestination, range);
        }

        _vk.CmdClearDepthStencilImage(CommandBuffer, vkTexture.Image.Value.Item1, vkTexture.NativeLayout, in clearValues, 1, in range);
    }

    public override void ClearBackground(Vector4 color)
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (CurrentFramebuffer is null)
        {
            throw new InvalidOperationException("Current framebuffer is null!");
        }

        if (CurrentFramebuffer.Attachments.Length == 0)
        {
            return;
        }

        if (CurrentFramebuffer.Attachments[0] is not VulkanTexture vkTexture)
        {
            throw new InvalidOperationException("Texture of framebuffer belongs to different backend!");
        }

        if (vkTexture.Image is null)
        {
            throw new InvalidOperationException("Texture of framebuffer is not loaded!");
        }

        var clearValue = new ClearColorValue(color.X, color.Y, color.Z, color.W);

        var range = new ImageSubresourceRange(
            ImageAspectFlags.ColorBit,
            0, Vk.RemainingMipLevels,
            0, Vk.RemainingArrayLayers);

        if (!vkTexture.TransitionLayout(this, TextureLayout.TransferDestination, range))
        {
            throw new InvalidOperationException("Unable to transition image layout!");
        }

        _vk.CmdClearColorImage(CommandBuffer, vkTexture.Image.Value.Item1, vkTexture.NativeLayout, &clearValue, 1, &range);
    }

    public override void BindVertexBuffer(DeviceBuffer? buffer, int slot)
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (slot < 0)
        {
            throw new ArgumentException("Passed negative binding slot!", nameof(slot));
        }

        ulong offset = 0;
        VkBuffer vkBuffer = new();

        if (buffer is not null)
        {
            if (buffer.Usage != BufferUsageType.Vertex)
            {
                throw new ArgumentException("Device buffer must be created for vertex use!", nameof(buffer));
            }

            if (buffer is not VulkanDeviceBuffer vkDeviceBuffer)
            {
                throw new ArgumentException("Device buffer belongs to different backend!", nameof(buffer));
            }

            vkBuffer = vkDeviceBuffer.Buffer;
        }

        _vk.CmdBindVertexBuffers(CommandBuffer, unchecked((uint)slot), 1, in vkBuffer, in offset);
    }

    public override void BindIndexBuffer(DeviceBuffer buffer, SpeedIndexType type)
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (buffer.Usage != BufferUsageType.Index)
        {
            throw new ArgumentException("Device buffer must be created for index use!", nameof(buffer));
        }

        if (buffer is not VulkanDeviceBuffer vkDeviceBuffer)
        {
            throw new ArgumentException("Device buffer belongs to different backend!", nameof(buffer));
        }

        _vk.CmdBindIndexBuffer(CommandBuffer, vkDeviceBuffer.Buffer, 0, type == SpeedIndexType.UInt16 ? VkIndexType.Uint16 : VkIndexType.Uint32);
    }

    public override void RenderInstances(SceneryInfo info)
    {
        throw new NotImplementedException();
    }

    public override void RenderSingle(SceneryInfo info)
    {
        throw new NotImplementedException();
    }

    public override void Draw(int vertexCount, int vertexOffset)
    {
        _vk.CmdDraw(CommandBuffer, unchecked((uint)vertexCount), 1, unchecked((uint)vertexOffset), 0);
    }

    public override void DrawIndexed(int indexCount, int vertexOffset, int indexOffset)
    {
        _vk.CmdDrawIndexed(CommandBuffer, unchecked((uint)indexCount), 1, unchecked((uint)indexOffset), vertexOffset, 0);
    }

    public override void DrawInstanced(int instanceCount, int vertexCount, int vertexOffset)
    {
        _vk.CmdDraw(CommandBuffer, unchecked((uint)vertexCount), unchecked((uint)instanceCount), unchecked((uint)vertexOffset), 0);
    }

    public override void DrawIndexedInstanced(int instanceCount, int indexCount, int vertexOffset, int indexOffset)
    {
        _vk.CmdDrawIndexed(CommandBuffer, unchecked((uint)indexCount), unchecked((uint)instanceCount), unchecked((uint)indexOffset), vertexOffset, 0);
    }

    void UpdatePipeline()
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (CurrentPipeline is not VulkanPipeline vkPipeline)
        {
            throw new InvalidOperationException("Provided pipeline is null or belongs to different backend!");
        }

        var bindPoint = VulkanTools.Convert(vkPipeline.Type);

        if (vkPipeline.Type == PipelineType.Graphics)
        {
            if (RenderSize is null)
            {
                throw new InvalidOperationException("Command buffer must be started with render size for binding graphics pipeline!");
            }

            UpdateRenderSize();
        }
        
        _vk.CmdBindPipeline(CommandBuffer, bindPoint, vkPipeline.Pipeline);
    }

    void UpdateRenderSize()
    {
        if (!Recording)
        {
            throw new InvalidOperationException("This action can be performed only while recording!");
        }

        if (!RenderSize.HasValue)
        {
            return;
        }

        var size = RenderSize.Value;

        var scissor = new Rect2D(new(), new(unchecked((uint)size.Item1), unchecked((uint)size.Item2)));
        var viewport = new Viewport(0, 0, size.Item1, size.Item2, 0, 1);

            _vk.CmdSetViewport(CommandBuffer, 0, 1, &viewport);
            _vk.CmdSetScissor(CommandBuffer, 0, 1, &scissor);
    }
}
