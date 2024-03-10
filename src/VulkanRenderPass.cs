using Silk.NET.Vulkan;
using Speed.Viewer.Render.Backend;
using System;
using System.Linq;
using System.Numerics;

using SpeedAttachmentDescription = Speed.Viewer.Render.Backend.AttachmentDescription;
using SpeedCommandBuffer = Speed.Viewer.Render.Backend.CommandBuffer;
using SpeedRenderPass = Speed.Viewer.Render.Backend.RenderPass;

using VkAttachmentDescription = Silk.NET.Vulkan.AttachmentDescription;
using VkRenderPass = Silk.NET.Vulkan.RenderPass;

namespace SilkVulkanModule;

internal unsafe sealed class VulkanRenderPass : SpeedRenderPass
{
    public override bool Started => _cmd is not null;

    internal VkRenderPass RenderPass { get; }

    VulkanCommandBuffer? _cmd;

    readonly Device _device;
    readonly Vk _vk;

    public VulkanRenderPass(Vk vk, params SpeedAttachmentDescription[] descriptions)
        : base(descriptions)
    {
        _vk = vk;
        _device = _vk.CurrentDevice!.Value;

        AttachmentReference reference = new(0, ImageLayout.ColorAttachmentOptimal);

        SubpassDescription subpass = new(
            SubpassDescriptionFlags.None,
            PipelineBindPoint.Graphics,
            0, null,
            1, &reference, null, null,
            0, null);

        SubpassDependency dependency = new(
            Vk.SubpassExternal, null,
            PipelineStageFlags.ColorAttachmentOutputBit, PipelineStageFlags.ColorAttachmentOutputBit,
            AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit, null,
            DependencyFlags.None);

        fixed (VkAttachmentDescription* pAttachments = descriptions.Select(x => Convert(x)).ToArray())
        {
            RenderPassCreateInfo createInfo = new()
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = unchecked((uint)descriptions.Length),
                PAttachments = pAttachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            VulkanTools.Ensure(_vk.CreateRenderPass(_device, in createInfo, null, out var renderPass));
            RenderPass = renderPass;
        }
    }

    public VulkanRenderPass(Vk vk, VkRenderPass renderPass, params SpeedAttachmentDescription[] descriptions)
        : base(descriptions)
    {
        _vk = vk;
        RenderPass = renderPass;
        _device = _vk.CurrentDevice!.Value;
    }

    public override void Dispose()
    {
        _vk.DestroyRenderPass(_device, RenderPass, null);
    }

    public override void Begin(SpeedCommandBuffer buffer, params ValueTuple<Vector4, float, uint>[] clearValues)
    {
        if (_cmd is not null)
        {
            throw new InvalidOperationException("Render pass already started.");
        }

        if (buffer is not VulkanCommandBuffer vkCommandBuffer)
        {
            throw new ArgumentException("Not compatible command buffer.", nameof(buffer));
        }

        if (buffer.CurrentFramebuffer is not VulkanFramebuffer vkFramebuffer)
        {
            throw new ArgumentException("Framebuffer not set.", nameof(buffer));
        }

        if (vkFramebuffer.Attachments.Length != 0 &&
            vkFramebuffer.Attachments[0] is VulkanTexture vkTexture &&
            vkTexture.Image.HasValue)
        {
            vkTexture.TransitionLayout(vkCommandBuffer, TextureLayout.ColorAttachment,
                new(ImageAspectFlags.ColorBit, 0, Vk.RemainingMipLevels, 0, Vk.RemainingArrayLayers));
        }

        RenderPassBeginInfo beginInfo = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = RenderPass,
            Framebuffer = vkFramebuffer.Framebuffer,
            RenderArea = new(new(), new(unchecked((uint)vkFramebuffer.Width), unchecked((uint)vkFramebuffer.Height)))
        };

        if (clearValues.Length > 0)
        {
            fixed (ClearValue* pValues = clearValues.Select(x => Convert(x)).ToArray())
            {
                beginInfo.ClearValueCount = unchecked((uint)clearValues.Length);
                beginInfo.PClearValues = pValues;
            }
        }

        _vk.CmdBeginRenderPass(vkCommandBuffer.CommandBuffer, in beginInfo, SubpassContents.Inline);
        _cmd = vkCommandBuffer;
    }

    public override void End()
    {
        if (_cmd is not null)
        {
            _vk.CmdEndRenderPass(_cmd.CommandBuffer);

            if (_cmd.CurrentFramebuffer is VulkanFramebuffer vkFramebuffer &&
                vkFramebuffer.Attachments.Length != 0 &&
                vkFramebuffer.Attachments[0] is VulkanTexture vkTexture)
            {
                vkTexture.Layout = TextureLayout.Present;
                vkTexture.NativeLayout = ImageLayout.PresentSrcKhr;
            }

            _cmd = null;
        }
    }

    static VkAttachmentDescription Convert(SpeedAttachmentDescription description)
    {
        return new()
        {
            Format = VulkanTools.Convert(description.Format),
            Samples = MapSamples(description.SampleCount),
            LoadOp = AttachmentLoadOp.DontCare,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
    }

    static ClearValue Convert(ValueTuple<Vector4, float, uint> clearValue)
    {
        return new()
        {
            Color = new(clearValue.Item1.X, clearValue.Item1.Y, clearValue.Item1.Z, clearValue.Item1.W),
            DepthStencil = new(clearValue.Item2, clearValue.Item3)
        };
    }

    static SampleCountFlags MapSamples(int sampleCount)
    {
        if (sampleCount > 0x7F)
        {
            throw new ArgumentException("Too many samples! Requested: " + sampleCount, nameof(sampleCount));
        }

        if (sampleCount < 0)
        {
            throw new ArgumentException("Requested negative number of samples.", nameof(sampleCount));
        }

        return (SampleCountFlags)sampleCount;
    }
}
