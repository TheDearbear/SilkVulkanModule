using Speed.Viewer.Modules;
using Speed.Viewer.Render;

namespace SilkVulkanModule;

public sealed class VulkanModule : Module
{
    VulkanRenderContextFactory? _factory;

    public VulkanModule()
        : base("Module.Vulkan", "Vulkan Renderer", "v1.0", null, ModuleLoadStage.Initialize) { }

    protected override void OnLoad()
    {
        if (_factory is null)
        {
            RenderManager.Factories.Add(_factory = new VulkanRenderContextFactory());
        }
    }

    protected override void OnUnload()
    {
        if (_factory is not null)
        {
            RenderManager.Factories.Remove(_factory);
            _factory = null;
        }
    }
}
