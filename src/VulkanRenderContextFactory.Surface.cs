using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Vulkan.Extensions.MVK;
using Speed.Viewer.Render.Backend;
using Speed.Viewer.Windowing;
using System;
using System.Diagnostics;

namespace VulkanModule;

internal unsafe sealed partial class VulkanRenderContextFactory
{
	delegate Result CreateSurfaceDelegate(Vk vk, IWindow window, ref RenderBackend backend);

	static Result CreateSurface(Vk vk, IWindow? window, ref RenderBackend backend)
	{
		CreateSurfaceDelegate createSurface;

		if (window is null)
		{
			return CreateHeadlessSurface(vk, ref backend);
		}

		if (OperatingSystem.IsWindows())
		{
			createSurface = CreateWin32Surface;
		}
		else if (OperatingSystem.IsAndroid())
		{
			createSurface = CreateAndroidSurface;
		}
		else if (OperatingSystem.IsMacOS())
		{
			createSurface = CreateMacOSSurface;
		}
		else if (OperatingSystem.IsIOS())
		{
			createSurface = CreateIOSSurface;
		}
		else if (window is IDisplayWindow && Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true)
		{
			createSurface = CreateWaylandSurface;
		}
		else if (window is IDisplayWindow && Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.Equals("x11", StringComparison.OrdinalIgnoreCase) == true)
		{
			createSurface = CreateX11Surface;
		}
		else
		{
			throw new PlatformNotSupportedException("Cannot detect supported OS!");
		}

		return createSurface(vk, window, ref backend);
	}

	static Result CreateWin32Surface(Vk vk, IWindow window, ref RenderBackend backend)
	{
		Win32SurfaceCreateInfoKHR createInfo = new()
		{
			SType = StructureType.Win32SurfaceCreateInfoKhr,
			Hinstance = Process.GetCurrentProcess().Handle,
			Hwnd = window.NativePointer
		};

		var khr = new KhrWin32Surface(vk.Context);

		Result result = khr.CreateWin32Surface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}

	static Result CreateAndroidSurface(Vk vk, IWindow window, ref RenderBackend backend)
	{
		var windowPtr = window.NativePointer;
		AndroidSurfaceCreateInfoKHR createInfo = new()
		{
			SType = StructureType.AndroidSurfaceCreateInfoKhr,
			Window = &windowPtr
		};

		var khr = new KhrAndroidSurface(vk.Context);

		Result result = khr.CreateAndroidSurface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}

	static Result CreateMacOSSurface(Vk vk, IWindow window, ref RenderBackend backend)
	{
		MacOSSurfaceCreateInfoMVK createInfo = new()
		{
			SType = StructureType.MacosSurfaceCreateInfoMvk,
			PView = window.NativePointer.ToPointer()
		};

		var mvk = new MvkMacosSurface(vk.Context);

		Result result = mvk.CreateMacOssurface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}

	static Result CreateIOSSurface(Vk vk, IWindow window, ref RenderBackend backend)
	{
		IOSSurfaceCreateInfoMVK createInfo = new()
		{
			SType = StructureType.IosSurfaceCreateInfoMvk,
			PView = window.NativePointer.ToPointer(),
		};

		var mvk = new MvkIosSurface(vk.Context);

		Result result = mvk.CreateIossurface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}

	static Result CreateWaylandSurface(Vk vk, IWindow window, ref RenderBackend backend)
	{
		if (window is not IDisplayWindow dWindow)
		{
			throw new ArgumentException("Provided window is not instance of " + nameof(IDisplayWindow), nameof(window));
		}

		var displayPtr = dWindow.Display;
		var surfacePtr = dWindow.NativePointer;

		WaylandSurfaceCreateInfoKHR createInfo = new()
		{
			SType = StructureType.WaylandSurfaceCreateInfoKhr,
			Display = &displayPtr,
			Surface = &surfacePtr
		};

		var khr = new KhrWaylandSurface(vk.Context);

		Result result = khr.CreateWaylandSurface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}

	static Result CreateX11Surface(Vk vk, IWindow window, ref RenderBackend backend)
	{
		if (window is not IDisplayWindow dWindow)
		{
			throw new ArgumentException("Provided window is not instance of " + nameof(IDisplayWindow), nameof(window));
		}

		var displayPtr = dWindow.Display;

		XlibSurfaceCreateInfoKHR createInfo = new()
		{
			SType = StructureType.XlibSurfaceCreateInfoKhr,
			Dpy = &displayPtr,
			Window = window.NativePointer
		};

		var khr = new KhrXlibSurface(vk.Context);

		Result result = khr.CreateXlibSurface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}

	static Result CreateHeadlessSurface(Vk vk, ref RenderBackend backend)
	{
		HeadlessSurfaceCreateInfoEXT createInfo = new()
		{
			SType = StructureType.HeadlessSurfaceCreateInfoExt
		};

		var headless = new ExtHeadlessSurface(vk.Context);

		Result result = headless.CreateHeadlessSurface(new(backend.BackendInstance), in createInfo, null, out var surface);
		backend.RenderSurface = new(unchecked((long)surface.Handle));

		return result;
	}
}
