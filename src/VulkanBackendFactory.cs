using Silk.NET.Vulkan;
using Speed.Engine.Render.Shaders;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using Speed.Viewer.Render.Backend.Pipelines;
using System.Linq;

using SpeedAttachmentDescription = Speed.Viewer.Render.Backend.AttachmentDescription;
using SpeedCommandBuffer = Speed.Viewer.Render.Backend.CommandBuffer;
using SpeedFramebuffer = Speed.Viewer.Render.Backend.Framebuffer;
using SpeedPipeline = Speed.Viewer.Render.Backend.Pipeline;
using SpeedRenderPass = Speed.Viewer.Render.Backend.RenderPass;

using VkCommandBuffer = Silk.NET.Vulkan.CommandBuffer;

namespace SilkVulkanModule;

internal unsafe sealed class VulkanBackendFactory : IBackendFactory
{
	readonly GraphicsPresentIndexPair _gpIndex;
	readonly PhysicalDevice _physicalDevice;
	readonly Device _device;
	readonly CommandPool _pool;
	readonly SurfaceKHR _surface;
    readonly Vk _vk;

	public VulkanBackendFactory(Vk vk, PhysicalDevice physDevice, SurfaceKHR surface, GraphicsPresentIndexPair gpIndex)
	{
        _vk = vk;
		_gpIndex = gpIndex;
		_physicalDevice = physDevice;
		_device = _vk.CurrentDevice!.Value;
		_surface = surface;

		_pool = CreateCommandPool();
	}

	public void Dispose()
	{
		_vk.DestroyCommandPool(_device, _pool, null);
	}

	public SpeedCommandBuffer[] CreateCommandBuffers(int count)
	{
		var allocInfo = new CommandBufferAllocateInfo()
		{
			SType = StructureType.CommandBufferAllocateInfo,
			CommandBufferCount = unchecked((uint)count),
			CommandPool = _pool,
			Level = CommandBufferLevel.Primary
		};

		VkCommandBuffer[] buffers = new VkCommandBuffer[count];
		fixed (VkCommandBuffer* pBuffers = buffers)
		{
			VulkanTools.Ensure(_vk.AllocateCommandBuffers(_device, in allocInfo, pBuffers));
		}

		return buffers.Select(buffer => new VulkanCommandBuffer(this, _vk, _pool, buffer)).ToArray();
	}

	public SpeedCommandBuffer CreateCommandBuffer()
	{
		return CreateCommandBuffers(1)[0];
	}

	public SpeedFramebuffer CreateFramebuffer(int width, int height, Texture[] attachments, SpeedRenderPass renderPass)
	{
		return new VulkanFramebuffer(_vk, width, height, attachments, renderPass);
	}

	public DeviceBuffer CreateDeviceBuffer(int size, BufferUsageType usage)
	{
		return new VulkanDeviceBuffer(_vk, _physicalDevice, size, usage);
	}

	public Shader CreateShader(ShaderType type, byte[] shader, string mainFunction)
	{
		return new VulkanShader(_vk, type, shader, mainFunction);
	}

	public Texture CreateTexture(TextureInfo info)
	{
		return new VulkanTexture(_vk, info);
	}

	public SpeedRenderPass CreateRenderPass(params SpeedAttachmentDescription[] descriptions)
	{
		return new VulkanRenderPass(_vk, descriptions);
	}

	public Swapchain CreateSwapchain(SwapchainInfo info, SpeedRenderPass renderPass)
	{
		return new VulkanSwapchain(_vk, _physicalDevice, _surface, info, renderPass);
	}

	public SpeedPipeline CreatePipeline(PipelineInfo info, SpeedRenderPass renderPass)
	{
		_vk.GetPhysicalDeviceFeatures(_physicalDevice, out var features);
		return new VulkanPipeline(_vk, features, info, renderPass);
	}

	CommandPool CreateCommandPool()
	{
		var poolCreateInfo = new CommandPoolCreateInfo()
		{
			SType = StructureType.CommandPoolCreateInfo,
			Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
			QueueFamilyIndex = _gpIndex.GraphicsFamilyIndex
		};

		VulkanTools.Ensure(_vk.CreateCommandPool(_device, in poolCreateInfo, null, out var pool));
		return pool;
	}
}
