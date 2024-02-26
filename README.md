# SilkVulkanModule
Vulkan API Render Module for Speed.Viewer Powered by Silk.NET.Vulkan

# TO-DO
### Port from Evergine's [bindings](../../../../EvergineTeam/Vulkan.NET)
- [ ] Main `VulkanRenderContextFactory` factory
  - [ ] `Create*Surface` methods
  - [ ] Everything else
- [ ] `VulkanModule` entrypoint
- [ ] `VulkanTexture` texture
  - [ ] Image transition
  - [ ] Everything else
- [ ] `VulkanFramebuffer` framebuffer
- [ ] `VulkanPipeline` pipeline
  - [ ] `Pipeline*StateCreateInfo` methods
  - [ ] Everything else
- [ ] Stub for `VulkanRenderContext` render context
- [ ] `VulkanSemaphore` semaphore
- [ ] `VulkanShader` shader
- [ ] `VulkanSwapchain` swapchain
- [ ] `VulkanRenderPass` render pass
- [ ] `VulkanDeviceBuffer` GPU memory
- [ ] `VulkanCommandBuffer` command buffer
- [ ] `VulkanBackendFactory` abstract factory
- [ ] `VulkanRenderer` renderer
### Implement
- [ ] Buffer allocation from heap for small allocations
- [ ] Allocated memory priority
