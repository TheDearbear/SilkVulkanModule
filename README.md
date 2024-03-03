# SilkVulkanModule
Vulkan API Render Module for Speed.Viewer Powered by [Silk.NET](../../../../dotnet/Silk.NET).Vulkan

# TO-DO
### Port from Evergine's [bindings](../../../../EvergineTeam/Vulkan.NET)
- [x] Main `VulkanRenderContextFactory` factory
  - [x] `Create*Surface` methods
  - [x] Everything else
- [x] `VulkanModule` entrypoint
- [x] `VulkanTexture` texture
  - [x] Image transition
  - [x] Everything else
- [x] `VulkanFramebuffer` framebuffer
- [x] `VulkanPipeline` pipeline
  - [x] `Pipeline*StateCreateInfo` methods
  - [x] Everything else
- [x] Stub for `VulkanRenderContext` render context
- [x] `VulkanSemaphore` semaphore
- [x] `VulkanShader` shader
- [ ] `VulkanSwapchain` swapchain
- [x] `VulkanRenderPass` render pass
- [x] `VulkanDeviceBuffer` GPU memory
- [x] `VulkanCommandBuffer` command buffer
- [ ] `VulkanBackendFactory` abstract factory
- [ ] `VulkanRenderer` renderer
### Implement
- [ ] Buffer allocation from heap for small allocations
- [ ] Allocated memory priority

# Building
This module requires local copies of `Speed.Viewer` and `Speed.Engine` in root directory.  
Use `dotnet build` or Visual Studio for building this module
