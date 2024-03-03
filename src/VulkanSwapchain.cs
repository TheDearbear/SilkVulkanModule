using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Speed.Viewer.Render.Backend;
using System;
using System.Linq;

using SpeedFramebuffer = Speed.Viewer.Render.Backend.Framebuffer;
using SpeedRenderPass = Speed.Viewer.Render.Backend.RenderPass;

namespace VulkanModule;

internal unsafe sealed class VulkanSwapchain : Swapchain
{
    internal SwapchainKHR Swapchain { get; private set; }
    internal uint CurrentImage { get; private set; }
    internal SpeedFramebuffer CurrentFramebuffer => _framebuffers.Length != 0 ?
        _framebuffers[CurrentImage] :
        throw new InvalidOperationException("No framebuffer present");

    SpeedFramebuffer[] _framebuffers = null!;
    SwapchainCapabilities _capabilities;
    SwapchainCreateInfoKHR _swapchainCreateInfo;
    Fence _fence;

    readonly VulkanRenderPass _renderPass;
    readonly SwapchainInfo _info;
    readonly SurfaceKHR _surface;
    readonly PhysicalDevice _physicalDevice;
    readonly Device _device;
    readonly KhrSwapchain _swapchainKhr;
    readonly KhrSurface _surfaceKhr;
    readonly Vk _vk;

    public VulkanSwapchain(Vk vk, PhysicalDevice physicalDevice, SurfaceKHR surface, SwapchainInfo info, SpeedRenderPass renderPass)
    {
        _vk = vk;
        _physicalDevice = physicalDevice;
        _surface = surface;
        _device = _vk.CurrentDevice!.Value;
        _info = info;

        if (!_vk.TryGetInstanceExtension(_vk.CurrentInstance!.Value, out _surfaceKhr))
        {
            throw new ArgumentException("Unable to obtain surface extension!", nameof(vk));
        }

        if (!_vk.TryGetDeviceExtension(_vk.CurrentInstance!.Value, _device, out _swapchainKhr))
        {
            throw new ArgumentException("Unable to obtain swapchain extension!", nameof(vk));
        }

        base.Resize(_info.Width, _info.Height);

        if (renderPass is not VulkanRenderPass vkRenderPass)
        {
            throw new ArgumentException("Render pass belongs to different backend.", nameof(renderPass));
        }

        _renderPass = vkRenderPass;
        _capabilities = GetSwapchainCapabilities();

        var requestedFormat = VulkanTools.Convert(_info.Format);

        var presentMode = PresentModeKHR.FifoKhr;

        var surfaceFormat = _capabilities.SupportedSurfaceFormats.FirstOrDefault(
            format => format.Format == requestedFormat && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr,
            _capabilities.SupportedSurfaceFormats[0]);

        _swapchainCreateInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = unchecked((uint)_info.RequiredImages),
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            // imageExtent will be filled in a copy of this instance in method CreateSwapchainAndFence
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit| ImageUsageFlags.TransferSrcBit| ImageUsageFlags.TransferDstBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = SurfaceTransformFlagsKHR.IdentityBitKhr,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true
        };

        CreateSwapchainAndFence(Width, Height);
    }

    public override void Dispose()
    {
        _swapchainKhr.DestroySwapchain(_device, Swapchain, null);
        _vk.DestroyFence(_device, _fence, null);
        Swapchain = new();
        _fence = new();

        foreach (var framebuffer in _framebuffers)
        {
            foreach (var image in framebuffer.Attachments.OfType<VulkanTexture>())
            {
                if (!image.Image.HasValue)
                {
                    continue;
                }

                // Do custom disposing (do not try to dispose swapchain's VkImage)
                _vk.DestroyImageView(_device, image.Image.Value.Item2, null);
            }

            framebuffer.Dispose();
        }

        _framebuffers = Array.Empty<SpeedFramebuffer>();
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);

        Dispose();

        _capabilities.SurfaceCapabilities = GetSurfaceCapabilities();
        CreateSwapchainAndFence(Width, Height);
    }

    public override SpeedFramebuffer NextImage()
    {
        uint index = 0;
        Fence fence = _fence;
        VulkanTools.Ensure(_vk.ResetFences(_device, 1, &fence));
        VulkanTools.Ensure(_swapchainKhr.AcquireNextImage(_device, Swapchain, 250_000_000, new(), fence, &index));
        VulkanTools.Ensure(_vk.WaitForFences(_device, 1, &fence, true, 250_000_000));
        CurrentImage = index;

        return _framebuffers[CurrentImage];
    }

    void CreateSwapchainAndFence(int width, int height)
    {
        if (Swapchain.Handle != 0)
        {
            throw new InvalidOperationException($"Cannot create new swapchain, previous swapchain still exists. (0x{Swapchain.Handle:X})");
        }

        if (_fence.Handle != 0)
        {
            _vk.DestroyFence(_device, _fence, null);
        }

        FenceCreateInfo fenceCreateInfo = new()
        {
            SType = StructureType.FenceCreateInfo
        };
        var createInfo = _swapchainCreateInfo with
        {
            ImageExtent = GetSupportedExtent(width, height)
        };

        VulkanTools.Ensure(_vk.CreateFence(_device, in fenceCreateInfo, null, out var fence));
        VulkanTools.Ensure(_swapchainKhr.CreateSwapchain(_device, in createInfo, null, out var swapchain));

        Swapchain = swapchain;
        _fence = fence;

        CreateFramebuffers();
    }

    void CreateFramebuffers()
    {
        uint count = 0;
        VulkanTools.Ensure(_swapchainKhr.GetSwapchainImages(_device, Swapchain, ref count, null));
        var images = new Image[count];
        fixed (Image* pImages = images)
        {
            VulkanTools.Ensure(_swapchainKhr.GetSwapchainImages(_device, Swapchain, ref count, pImages));
        }

        _framebuffers = new SpeedFramebuffer[count];
        for (uint i = 0; i < count; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = images[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainCreateInfo.ImageFormat,
                Components = new(),
                SubresourceRange = new(ImageAspectFlags.ColorBit, 0, Vk.RemainingMipLevels, 0, Vk.RemainingArrayLayers)
            };

            VulkanTools.Ensure(_vk.CreateImageView(_device, in createInfo, null, out var view));

            var info = new TextureInfo()
            {
                Width = Width,
                Height = Height,
                MipLevels = 1,
                Samples = 1,
                Format = _info.Format,
                Layout = TextureLayout.Undefined,
                Tiling = TextureTiling.Optimal,
                Usage = TextureUsage.ColorAttachment
            };

            var texture = new VulkanTexture(_vk, images[i], view, info);

            _framebuffers[i] = new VulkanFramebuffer(_vk, Width, Height, new[] { texture }, _renderPass);
        }
    }

    Extent2D GetSupportedExtent(int width, int height)
    {
        var capabilities = _capabilities.SurfaceCapabilities;

        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }

        return new(Math.Clamp(unchecked((uint)width), capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Math.Clamp(unchecked((uint)height), capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height));
    }

    SwapchainCapabilities GetSwapchainCapabilities()
    {
        var surfaceCapabilities = GetSurfaceCapabilities();

        uint count = 0;
        VulkanTools.Ensure(_surfaceKhr.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, ref count, null));
        var surfaceFormats = new SurfaceFormatKHR[count];
        fixed (SurfaceFormatKHR* pSurfaceFormats = surfaceFormats)
        {
            VulkanTools.Ensure(_surfaceKhr.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, ref count, pSurfaceFormats));
        }

        VulkanTools.Ensure(_surfaceKhr.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, ref count, null));
        var presentModes = new PresentModeKHR[count];
        fixed (PresentModeKHR* pPresentModes = presentModes)
        {
            VulkanTools.Ensure(_surfaceKhr.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, ref count, pPresentModes));
        }

        return new(surfaceCapabilities, surfaceFormats, presentModes);
    }

    SurfaceCapabilitiesKHR GetSurfaceCapabilities()
    {
        VulkanTools.Ensure(_surfaceKhr.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var surfaceCapabilities));

        return surfaceCapabilities;
    }
}
