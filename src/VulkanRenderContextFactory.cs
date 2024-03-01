using Serilog;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Speed.Engine.Camera;
using Speed.Engine.Logging;
using Speed.Engine.Render;
using Speed.Viewer;
using Speed.Viewer.Render;
using Speed.Viewer.Render.Backend;
using Speed.Viewer.Windowing;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VulkanModule;

internal unsafe sealed partial class VulkanRenderContextFactory : IRenderContextFactory
{
	public string[]? SupportedBackends { get; } = new[] { "Vulkan" };

	readonly static string[] _deviceExtensions = new[]
	{
		KhrSwapchain.ExtensionName,
		KhrTimelineSemaphore.ExtensionName,
	};

	readonly static string[] _instanceExtensions = new[]
	{
		ExtDebugUtils.ExtensionName
	};

	const string ENGINE_NAME = "Speed Engine";
	const string APPLICATION_NAME = "Speed Viewer";

	public bool CreateRenderContext(IWindow window, RenderBackend backend, ICamera camera, ILogger? logger, [NotNullWhen(true)] out ValueTuple<IRenderContext, IRenderer>? output)
	{
		var factoryLogger = logger?.ForContextShortName<VulkanRenderContextFactory>();

		if (backend.FourCC != CharCodes.Vulkan)
		{
			factoryLogger?.Fatal(new ArgumentException("Backend FourCC mismatch", nameof(backend)),
				"Expected Vulkan's FourCC ({VulkanFourCC}), got {GivenFourCC}",
				CharCodes.Vulkan,
				backend.FourCC == CharCodes.Null ? "*Null*" : backend.FourCC);

			output = null;
			return false;
		}

		Vk vk = Vk.GetApi();
		Instance instance = new(backend.BackendInstance);

		uint count = 0;
		VulkanTools.Ensure(vk.EnumeratePhysicalDevices(instance, ref count, null));
		if (count == 0)
		{
			factoryLogger?.Error("Cannot find any physical devices for using by Vulkan!");
			output = null;
			return false;
		}

		Span<PhysicalDevice> devices = new PhysicalDevice[count];
		VulkanTools.Ensure(vk.EnumeratePhysicalDevices(instance, &count, devices));

		SurfaceKHR vkSurface = new(unchecked((ulong)backend.RenderSurface.ToInt64()));

		List<GCHandle> handles = new(_deviceExtensions.Length + 2);

		int deviceIndex = LaunchOptions.GPUIndex is not null && LaunchOptions.GPUIndex >= 0 && LaunchOptions.GPUIndex < count ? LaunchOptions.GPUIndex.Value : 0;
		DeviceQueueCreateInfo[] queuesCreateInfo = GenerateDeviceQueueCreateInfoArray(vk, devices[deviceIndex], vkSurface, handles, out var queues);

		fixed (DeviceQueueCreateInfo* pQueuesCreateInfo = queuesCreateInfo)
		{
			PhysicalDeviceTimelineSemaphoreFeatures timelineSemaphore = new()
			{
				SType = StructureType.PhysicalDeviceTimelineSemaphoreFeatures,
				TimelineSemaphore = true
			};

			DeviceCreateInfo createInfo = new()
			{
				SType = StructureType.DeviceCreateInfo,
				PNext = &timelineSemaphore,
				QueueCreateInfoCount = unchecked((uint)queuesCreateInfo.Length),
				PQueueCreateInfos = pQueuesCreateInfo,
				EnabledExtensionCount = unchecked((uint)_deviceExtensions.Length),
				PpEnabledExtensionNames = GetDeviceExtensions(vk, devices[deviceIndex], handles)
			};

			VulkanTools.Ensure(vk.CreateDevice(devices[deviceIndex], in createInfo, null, out var device));
		}

		foreach (var handle in handles)
		{
			handle.Free();
		}

		vk.GetPhysicalDeviceProperties(devices[deviceIndex], out var props);
		
		factoryLogger?.Debug("Using GPU with name: {GPUName}", VulkanTools.ConvertUTF8(props.DeviceName));
		(factoryLogger as IDisposable)?.Dispose();

		// TODO: Port VulkanRenderContext and VulkanRenderer
		//var ctx = new VulkanRenderContext(camera, logger, device, instance);
		//output = new(ctx, new VulkanRenderer(devices[deviceIndex], device, vkSurface, queues, window, ctx));
		output = null;
		return true;
	}

	public bool CreateBackend(IWindow window, FourCC fourCC, [NotNullWhen(true)] out RenderBackend? backend)
	{
		if (fourCC != CharCodes.Vulkan)
		{
			backend = null;
			return false;
		}

		uint vkVersion = 0;
		Version32 version = Vk.Version11;

		var vk = Vk.GetApi();
		VulkanTools.Ensure(vk.EnumerateInstanceVersion(ref vkVersion));
		if (vkVersion < version)
		{
			var factoryLogger = Log.Logger.ForContextShortName<VulkanRenderContextFactory>();
			factoryLogger.Error("Current system cannot support request Vulkan version ({RequestedVersion}). Supported version: {SupportedVersion}", "1.1", GetVulkanVersion(vkVersion));
			(factoryLogger as IDisposable)?.Dispose();

			backend = null;
			return false;
		}

		VulkanTools.Ensure(CreateInstanceAndSurface(vk, window, version, _instanceExtensions, out var vkBackend));

		vkBackend.Version = new(version.Major, version.Minor, version.Patch);
		backend = vkBackend;
		
		return true;
	}

	public void DestroyBackend(IWindow window, RenderBackend backend)
	{
		if (backend.FourCC != CharCodes.Vulkan)
		{
			return;
		}

		Vk vk = Vk.GetApi();
		var khrSurface = new KhrSurface(vk.Context);

		if (backend.RenderSurface != IntPtr.Zero)
		{
			khrSurface.DestroySurface(new(backend.BackendInstance), new(unchecked((ulong)backend.RenderSurface.ToInt64())), null);
		}

		if (backend.BackendInstance != IntPtr.Zero)
		{
			vk.DestroyInstance(new(backend.BackendInstance), null);
		}
	}

	static Result CreateInstanceAndSurface(Vk vk, IWindow window, Version32 apiVersion, IEnumerable<string> optionalExtensions, out RenderBackend backend)
	{
		List<GCHandle> handles = new(3 + optionalExtensions.Count());

		var (applicationName, engineName) = GetApplicationInfo(handles);
		ApplicationInfo info = new()
		{
			SType = StructureType.ApplicationInfo,
			PApplicationName = (byte*)applicationName.ToPointer(),
			PEngineName = (byte*)engineName.ToPointer(),
			ApiVersion = apiVersion
		};

		var (extensionCount, ppExtensions) = GetInstanceExtensions(window, handles, optionalExtensions);
		InstanceCreateInfo instanceCreateInfo = new()
		{
			SType = StructureType.InstanceCreateInfo,
			PApplicationInfo = &info,
			EnabledExtensionCount = extensionCount,
			PpEnabledExtensionNames = (byte**)ppExtensions.ToPointer()
		};

		VulkanTools.Ensure(vk.CreateInstance(in instanceCreateInfo, null, out var vkInstance));
		vk.CurrentInstance = vkInstance;
		backend = new()
		{
			FourCC = CharCodes.Vulkan,
			BackendInstance = vkInstance.Handle
		};

		foreach (var handle in handles)
		{
			handle.Free();
		}

		return CreateSurface(vk, window, ref backend);
	}

	static GraphicsPresentIndexPair GetQueueFamilyIndex(Vk vk, PhysicalDevice physicalDevice, SurfaceKHR surface)
	{
		uint? graphics = null;
		uint? present = null;

		uint count = 0;
		vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref count, null);

		Span<QueueFamilyProperties> arr = new QueueFamilyProperties[unchecked((int)count)];
		vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, arr);

		var khrSurface = new KhrSurface(vk.Context);

		for (uint i = 0; i < count && (graphics is null || present is null); i++)
		{
			if (graphics is null && arr[unchecked((int)i)].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
			{
				graphics = i;
			}

			if (present is null)
			{
				VulkanTools.Ensure(khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface, out var supported));

				if (supported)
				{
					present = i;
				}
			}
		}

		if (graphics is null || present is null)
		{
			var logger = Log.Logger.ForContextShortName<VulkanRenderContextFactory>();

			if (graphics is null)
			{
				logger.Error("Unable to find graphics queue family index!");
			}
			else // present is null
			{
				logger.Error("Unable to find present queue family index!");
			}

			(logger as IDisposable)?.Dispose();
		}

		return new()
		{
			GraphicsFamilyIndex = graphics ?? default,
			PresentFamilyIndex = present ?? default
		};
	}

	static DeviceQueueCreateInfo[] GenerateDeviceQueueCreateInfoArray(Vk vk, PhysicalDevice physicalDevice, SurfaceKHR surface, IList<GCHandle> handles, out GraphicsPresentIndexPair pair)
	{
		pair = GetQueueFamilyIndex(vk, physicalDevice, surface);
		
		float[] priorities = new[] { 1f, 1f };
		GCHandle handle = GCHandle.Alloc(priorities, GCHandleType.Pinned);
		handles.Add(handle);
		float* pPriorities = (float*)handle.AddrOfPinnedObject().ToPointer();

		if (pair.GraphicsFamilyIndex == pair.PresentFamilyIndex)
		{
			return new[]
			{
				new DeviceQueueCreateInfo
				{
					SType = StructureType.DeviceQueueCreateInfo,
					PQueuePriorities = pPriorities,
					QueueCount = 2,
					QueueFamilyIndex = pair.GraphicsFamilyIndex
				}
			};
		}

		return new[]
		{
			new DeviceQueueCreateInfo
			{
				SType = StructureType.DeviceQueueCreateInfo,
				PQueuePriorities = pPriorities,
				QueueCount = 1,
				QueueFamilyIndex = pair.PresentFamilyIndex
			},
			new DeviceQueueCreateInfo
			{
				SType = StructureType.DeviceQueueCreateInfo,
				PQueuePriorities = pPriorities + 1,
				QueueCount = 1,
				QueueFamilyIndex = pair.GraphicsFamilyIndex
			}
		};
	}

	static ValueTuple<IntPtr, IntPtr> GetApplicationInfo(IList<GCHandle> handles)
	{
		byte[] applicationName = Encoding.UTF8.GetBytes(APPLICATION_NAME + '\0');
		byte[] engineName = Encoding.UTF8.GetBytes(ENGINE_NAME + '\0');

		GCHandle applicationNameHandle = GCHandle.Alloc(applicationName, GCHandleType.Pinned);
		GCHandle engineNameHandle = GCHandle.Alloc(engineName, GCHandleType.Pinned);

		handles.Add(applicationNameHandle);
		handles.Add(engineNameHandle);

		return new(applicationNameHandle.AddrOfPinnedObject(),
			engineNameHandle.AddrOfPinnedObject());
	}

	static ValueTuple<uint, IntPtr> GetInstanceExtensions(IWindow window, IList<GCHandle> handles, IEnumerable<string> additionalExtensions)
	{
		var logger = Log.Logger.ForContextShortName<VulkanRenderContextFactory>();
		
		string[] extensions = WindowingManager.GetRequiredSurfaceExtensions(window, CharCodes.Vulkan).Concat(additionalExtensions).ToArray();
		IntPtr[] pExtensions = new IntPtr[extensions.Length];

		int i = 0;
		foreach (string extension in extensions)
		{
			logger.Verbose("Enabling Instance Extension: {Extension}", extension);

			byte[] bytes = Encoding.UTF8.GetBytes(extension + '\0');
			GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			handles.Add(handle);

			pExtensions[i++] = handle.AddrOfPinnedObject();
		}

		GCHandle arrayHandle = GCHandle.Alloc(pExtensions, GCHandleType.Pinned);
		handles.Add(arrayHandle);

		(logger as IDisposable)?.Dispose();

		return new(unchecked((uint)extensions.Length), arrayHandle.AddrOfPinnedObject());
	}

	static byte** GetDeviceExtensions(Vk vk, PhysicalDevice physicalDevice, IList<GCHandle> handles)
	{
		var logger = Log.Logger.ForContextShortName<VulkanRenderContextFactory>();

		uint deviceExtCount = 0;
		VulkanTools.Ensure(vk.EnumerateDeviceExtensionProperties(physicalDevice, null as string, ref deviceExtCount, null));
		
		Span<ExtensionProperties> extProps = new ExtensionProperties[unchecked((int)deviceExtCount)];
		fixed (ExtensionProperties* pExtProps = extProps)
		{
			VulkanTools.Ensure(vk.EnumerateDeviceExtensionProperties(physicalDevice, null as string, &deviceExtCount, pExtProps));
		}

		var stringExtProps = extProps.ToArray().Select(prop => VulkanTools.ConvertUTF8(prop.ExtensionName));

		IntPtr[] pExtensions = new IntPtr[_deviceExtensions.Length];
		int i = 0;
		foreach (var extension in _deviceExtensions)
		{
			if (!stringExtProps.Contains(extension))
			{
				throw new PlatformNotSupportedException("Device extension not supported: " + extension);
			}

			logger.Verbose("Enabling Device Extension: {Extension}", extension);

			byte[] ext = Encoding.UTF8.GetBytes(extension + '\0');
			GCHandle handle = GCHandle.Alloc(ext, GCHandleType.Pinned);
			handles.Add(handle);

			pExtensions[i++] = handle.AddrOfPinnedObject();
		}

		(logger as IDisposable)?.Dispose();

		GCHandle arrayHandle = GCHandle.Alloc(pExtensions, GCHandleType.Pinned);
		handles.Add(arrayHandle);

		return (byte**)arrayHandle.AddrOfPinnedObject().ToPointer();
	}

	static string GetVulkanVersion(uint version)
	{
		var variant = version >> 29;
		var major = (version >> 22) & 0x7F;
		var minor = (version >> 12) & 0x3FF;
		var patch = version & 0xFFF;

		string prefix = "Vulkan";
		if (variant != 0)
		{
			prefix += $" (Variant {variant})";
		}

		return $"{prefix} {major}.{minor}.{patch}";
	}
}
