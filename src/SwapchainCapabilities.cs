using Silk.NET.Vulkan;
using System;

namespace VulkanModule;

internal struct SwapchainCapabilities
{
	public SurfaceCapabilitiesKHR SurfaceCapabilities;
	public SurfaceFormatKHR[] SupportedSurfaceFormats;
	public PresentModeKHR[] SupportedPresentModes;

	public SwapchainCapabilities()
	{
		SurfaceCapabilities = default;
		SupportedSurfaceFormats = Array.Empty<SurfaceFormatKHR>();
		SupportedPresentModes = Array.Empty<PresentModeKHR>();
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
