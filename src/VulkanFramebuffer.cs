using Silk.NET.Vulkan;
using Speed.Engine.Textures;
using System;
using System.Linq;

using SpeedRenderPass = Speed.Viewer.Render.Backend.RenderPass;
using SpeedFramebuffer = Speed.Viewer.Render.Backend.Framebuffer;

using VkFramebuffer = Silk.NET.Vulkan.Framebuffer;

namespace SilkVulkanModule;

internal sealed unsafe class VulkanFramebuffer : SpeedFramebuffer
{
    internal VkFramebuffer Framebuffer { get; set; }

    readonly VulkanTexture[] _attachments;
    readonly VulkanRenderPass _renderPass;
    readonly Device _device;
    readonly Vk _vk;

    public VulkanFramebuffer(Vk vk, int width, int height, Texture[] attachments, SpeedRenderPass renderPass)
        : base(width, height, attachments, renderPass)
    {
        _vk = vk;
        _device = _vk.CurrentDevice!.Value;

        if (renderPass is not VulkanRenderPass vkRenderPass)
        {
            throw new ArgumentException("Provided render pass belongs to different backend.", nameof(renderPass));
        }

        VulkanTexture[] vkAttachments = attachments.OfType<VulkanTexture>().ToArray();
        if (vkAttachments.Length != attachments.Length)
        {
            throw new ArgumentException("Some of attachments belongs to different backends.", nameof(attachments));
        }

        _attachments = vkAttachments;
        _renderPass = vkRenderPass;

        ImageView[] views = new ImageView[_attachments.Length];
        for (int i = 0; i < views.Length; i++)
        {
            var img = _attachments[i].Image;
            if (!img.HasValue)
            {
                throw new ArgumentException($"One of framebuffer attachments not initialized! (Attachment {i + 1})", nameof(attachments));
            }

            views[i] = img.Value.Item2;
        }

        fixed (ImageView* pViews = views)
        {
            FramebufferCreateInfo createInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass.RenderPass,
                AttachmentCount = unchecked((uint)_attachments.Length),
                PAttachments = pViews,
                Width = unchecked((uint)Width),
                Height = unchecked((uint)Height),
                Layers = 1
            };

            VulkanTools.Ensure(_vk.CreateFramebuffer(_device, in createInfo, null, out var framebuffer));
            Framebuffer = framebuffer;
        }
    }

    public override void Dispose()
    {
        _vk.DestroyFramebuffer(_device, Framebuffer, null);
    }
}
