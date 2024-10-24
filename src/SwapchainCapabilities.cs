using Silk.NET.Vulkan;

namespace SilkVulkanModule;

internal struct SwapchainCapabilities
{
	public SurfaceCapabilitiesKHR SurfaceCapabilities;
	public SurfaceFormatKHR[] SupportedSurfaceFormats;
	public PresentModeKHR[] SupportedPresentModes;

	public SwapchainCapabilities()
	{
		SurfaceCapabilities = default;
		SupportedSurfaceFormats = [];
		SupportedPresentModes = [];
	}

	public SwapchainCapabilities(SurfaceCapabilitiesKHR surfaceCapabilities,
		SurfaceFormatKHR[] surfaceFormats,
		PresentModeKHR[] presentModes)
	{
		SurfaceCapabilities = surfaceCapabilities;
		SupportedSurfaceFormats = surfaceFormats;
		SupportedPresentModes = presentModes;
	}
}
