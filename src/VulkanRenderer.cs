using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Speed.Engine.Render;
using Speed.Viewer.Render.Backend;
using System;
using System.Collections.Generic;
using System.Linq;

using SpeedCommandBuffer = Speed.Viewer.Render.Backend.CommandBuffer;
using SpeedFramebuffer = Speed.Viewer.Render.Backend.Framebuffer;

using VkCommandBuffer = Silk.NET.Vulkan.CommandBuffer;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace SilkVulkanModule;

internal sealed class VulkanRenderer : IRenderer
{
    public IBackendFactory BackendFactory { get; }

    public IRenderContext Context { get; }

    public bool ShadersSupported { get; } = true;

    public bool RenderPassSupported { get; } = true;

    readonly Dictionary<VkCommandBuffer, Fence> _submitFences = [];

    readonly VulkanCommandBuffer _cmd;
    readonly GraphicsPresentIndexPair _gpIndex;
    readonly PhysicalDevice _physicalDevice;
    readonly Device _device;
    readonly Queue _graphicsQueue;
    readonly Queue _presentQueue;
    readonly KhrSwapchain _swapchainKhr;
    readonly Vk _vk;

    public unsafe VulkanRenderer(Vk vk, PhysicalDevice physDevice, SurfaceKHR surface, GraphicsPresentIndexPair gpIndex, IRenderContext ctx)
    {
        _gpIndex = gpIndex;
        _physicalDevice = physDevice;
        _device = vk.CurrentDevice!.Value;
        Context = ctx;
        
        _vk = vk;
        if (!_vk.TryGetDeviceExtension(_vk.CurrentInstance!.Value, _device, out _swapchainKhr))
        {
            throw new ArgumentException("Unable to obtain Vulkan swapchain extension!", nameof(vk));
        }

        BackendFactory = new VulkanBackendFactory(_vk, _physicalDevice, surface, gpIndex);

        _vk.GetDeviceQueue(_device, _gpIndex.GraphicsFamilyIndex, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_device, _gpIndex.PresentFamilyIndex, 0, out _presentQueue);

        _cmd = (VulkanCommandBuffer)BackendFactory.CreateCommandBuffer();
    }

    public void Dispose()
    {
        _cmd.Dispose();
        BackendFactory.Dispose();
    }

    public unsafe void WaitForEnd(params SpeedCommandBuffer[] buffers)
    {
        ulong[] values = new ulong[buffers.Length];
        VkSemaphore[] semaphores = new VkSemaphore[buffers.Length];

        fixed (ulong* pValues = values)
        fixed (VkSemaphore* pSemaphores = semaphores)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                if (buffers[i] is not VulkanCommandBuffer vkCmd)
                {
                    throw new ArgumentException("One of command buffers belongs to other backend!", nameof(buffers));
                }

                pSemaphores[i] = vkCmd.Semaphore.Semaphore;
            }

            var waitInfo = new SemaphoreWaitInfo()
            {
                SType = StructureType.SemaphoreWaitInfo,
                SemaphoreCount = unchecked((uint)buffers.Length),
                PSemaphores = pSemaphores,
                PValues = pValues
            };

            VulkanTools.Ensure(_vk.WaitSemaphores(_device, in waitInfo, 100_000_000_000)); // Wait 100 seconds
        }
    }

    public void WaitIdle()
    {
        VulkanTools.Ensure(_vk.DeviceWaitIdle(_device));
    }

    public unsafe void Submit(params SpeedCommandBuffer[] buffers)
    {
        VkCommandBuffer* pCmd = stackalloc VkCommandBuffer[buffers.Length + 1];

        for (int i = 0; i < buffers.Length; i++)
        {
            SpeedCommandBuffer buffer = buffers[i];

            if (buffer is not VulkanCommandBuffer vkCmd)
            {
                throw new ArgumentException("One of command buffers belongs to other backend!", nameof(buffers));
            }

            pCmd[i] = vkCmd.CommandBuffer;
        }

        _cmd.Begin();
        var withFramebuffer = buffers.Select(x => x.CurrentFramebuffer).OfType<SpeedFramebuffer>().ToArray();
        ImageMemoryBarrier* pBarriers = stackalloc ImageMemoryBarrier[withFramebuffer.Length];
        
        for (int i = 0; i < withFramebuffer.Length; i++)
        {
            var vkTexture = (VulkanTexture)withFramebuffer[i].Attachments[0];
            if (vkTexture.Image is null)
            {
                throw new InvalidOperationException("Framebuffer's image is not loaded!");
            }

            pBarriers[i] = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = VulkanTools.GetAccessFlags(vkTexture.NativeLayout),
                DstAccessMask = 0,
                OldLayout = vkTexture.NativeLayout,
                NewLayout = ImageLayout.PresentSrcKhr,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = vkTexture.Image.Value.Item1,
                SubresourceRange = new(ImageAspectFlags.ColorBit, 0, Vk.RemainingMipLevels, 0, Vk.RemainingArrayLayers)
            };
        }

        _vk.CmdPipelineBarrier(_cmd.CommandBuffer,
            PipelineStageFlags.AllGraphicsBit, PipelineStageFlags.AllGraphicsBit,
            DependencyFlags.ByRegionBit,
            0, null,
            0, null,
            unchecked((uint)withFramebuffer.Length), pBarriers);
        _cmd.End();
        pCmd[buffers.Length] = _cmd.CommandBuffer;

        var submitInfo = new SubmitInfo()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = unchecked((uint)buffers.Length),
            PCommandBuffers = pCmd
        };

        VulkanTools.Ensure(_vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, new()));

        foreach (var cmd in buffers)
        {
            var vkCmd = (VulkanCommandBuffer)cmd;

            foreach (var memory in vkCmd.DisposeQueue)
            {
                memory.Dispose();
            }

            vkCmd.DisposeQueue.Clear();
        }
    }

    public unsafe void Present(params Swapchain[] swapchains)
    {
        SwapchainKHR* pSwapchain = stackalloc SwapchainKHR[swapchains.Length];
        uint* pIndices = stackalloc uint[swapchains.Length];

        for (int i = 0; i < swapchains.Length; i++)
        {
            var swapchain = swapchains[i];
            if (swapchain is not VulkanSwapchain vkSwapchain)
            {
                throw new ArgumentException("One of swapchains belongs to other backend!", nameof(swapchains));
            }

            pSwapchain[i] = vkSwapchain.Swapchain;
            pIndices[i] = vkSwapchain.CurrentImage;
        }

        var presentInfo = new PresentInfoKHR()
        {
            SType = StructureType.PresentInfoKhr,
            SwapchainCount = unchecked((uint)swapchains.Length),
            PSwapchains = pSwapchain,
            PImageIndices = pIndices
        };

        VulkanTools.Ensure(_swapchainKhr.QueuePresent(_presentQueue, in presentInfo));
    }
}
