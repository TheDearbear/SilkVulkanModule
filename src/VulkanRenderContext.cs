using Serilog;
using Serilog.Events;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Speed.Engine;
using Speed.Engine.Camera;
using Speed.Engine.Input;
using Speed.Engine.Logging;
using Speed.Engine.Render;
using Speed.Engine.Render.Regions;
using Speed.Engine.Sceneries;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Threading.Tasks;

namespace SilkVulkanModule;

internal unsafe sealed class VulkanRenderContext : IRenderContext
{
    readonly List<IRenderRegion> _regions = [];
    public IEnumerable<IRenderRegion> Regions => _regions;

    public ICamera RenderCamera { get; set; }

    ICamera? _mathCamera;
    [AllowNull]
    public ICamera MathCamera
    {
        get => _mathCamera ?? RenderCamera;
        set => _mathCamera = value;
    }
    
    public int CurrentRegion { get; set; }
    
    public bool Render { get; set; }
    
    public Vector4 BackgroundColor { get; set; }

    public IReadOnlyDictionary<int, Scenery> RenderedSceneries { get; private set; } = new Dictionary<int, Scenery>();

    ILogger? _logger;
    public ILogger? Logger
    {
        get => _logger;
        set => _logger = value?.ForContextShortName<VulkanRenderContext>();
    }

    public event EventHandler<ValueTuple<double, FrameInput?>>? NewLogicFrame;
    public event EventHandler<double>? NewRenderFrame;

    readonly Vk _vk;
    readonly ExtDebugUtils _debugUtils;
    readonly Device _device;
    readonly Instance _instance;
    readonly VulkanRenderer _renderer;

    PfnDebugUtilsMessengerCallbackEXT? _debugCallback;
    DebugUtilsMessengerEXT? _messenger;

    public VulkanRenderContext(VulkanRenderer renderer, ICamera camera, ILogger? logger, Vk vk)
    {
        Logger = logger;
        RenderCamera = camera;
        _renderer = renderer;
        _vk = vk;

        _device = _vk.CurrentDevice!.Value;
        _instance = _vk.CurrentInstance!.Value;

        if (!_vk.TryGetInstanceExtension(_instance, out _debugUtils))
        {
            throw new ArgumentException("Cannot obtain Debug Utils extension!", nameof(vk));
        }
#if DEBUG

        RegisterDebugMessenger();
        TestDebugMessenger();
#endif
    }

    public int CreateRegion()
    {
        throw new NotImplementedException();
    }

    public SceneryInfo CreateSceneryInfo()
    {
        throw new NotImplementedException();
    }

    public Solid CreateSolid()
    {
        throw new NotImplementedException();
    }

    // Should be called only by Speed.Engine-level
    // code for creating ONLY objects' texture
    public Texture CreateTexture()
    {
        return _renderer.BackendFactory.CreateTexture(new()
        {
            Samples = 1,
            Tiling = TextureTiling.Optimal,
            Usage = TextureUsage.Sampled
        });
    }

    public void Dispose()
    {
        _renderer.Dispose();

        _vk.DestroyDevice(_device, null);
#if DEBUG
        UnregisterDebugMessenger();
#endif
    }

    public bool DisposeAndRemoveRegion(int index)
    {
        throw new NotImplementedException();
    }

    public void DoLogic(double delta, FrameInput? input)
    {
        NewLogicFrame?.Invoke(this, new(delta, input));
    }

    public void DoRender(double delta)
    {
        NewRenderFrame?.Invoke(this, delta);
    }

    public Task<PickingResult> PickInstance(int x, int y)
    {
        throw new NotImplementedException();
    }

    public bool RemoveRegion(int index)
    {
        throw new NotImplementedException();
    }

    public void Resize(uint width, uint height)
    {
        MathCamera.AspectRatio = RenderCamera.AspectRatio = (float)((double)width / height);
    }

    static DebugUtilsMessengerCallbackFunctionEXT CreateDebugMessengerCallback(ILogger loggerInstance)
    {
        ILogger logger = loggerInstance.ForContextShortName("Vulkan Debug");

        uint DebugUtilsMessengerCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
            DebugUtilsMessageTypeFlagsEXT messageTypes,
            DebugUtilsMessengerCallbackDataEXT* pCallbackData,
            void* pUserData)
        {
            string format = "[{MessageId}/{MessageIdName}] [{Type}] {Message}";

            var zeroPtr = IntPtr.Zero.ToPointer();

            string idName = pCallbackData->PMessageIdName != zeroPtr ? VulkanTools.ConvertUTF8(pCallbackData->PMessageIdName) : string.Empty;
            string message = pCallbackData->PMessage != zeroPtr ? VulkanTools.ConvertUTF8(pCallbackData->PMessage) : string.Empty;

            string typeStr = messageTypes switch
            {
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt => "General",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt => "Validation",
                DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "Performance",
                DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt => "Device address binding",
                _ => "Unknown"
            };

            LogEventLevel level = messageSeverity switch
            {
                DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt => LogEventLevel.Verbose,
                DebugUtilsMessageSeverityFlagsEXT.InfoBitExt => LogEventLevel.Information,
                DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => LogEventLevel.Warning,
                DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => LogEventLevel.Error,
                _ => LogEventLevel.Debug
            };

            logger.Write(level, format, pCallbackData->MessageIdNumber, idName, typeStr, message);

            return 0;
        }

        return DebugUtilsMessengerCallback;
    }

    void TestDebugMessenger()
    {
        DebugUtilsObjectNameInfoEXT obj = new()
        {
            SType = StructureType.DebugUtilsObjectNameInfoExt,
            ObjectType = ObjectType.Unknown
        };

        fixed (byte* msgIdName = "Debug test\0"u8)
        fixed (byte* testMsg = "This is a test message for debug system.\0"u8)
        {
            DebugUtilsMessengerCallbackDataEXT callbackData = new()
            {
                SType = StructureType.DebugUtilsMessengerCallbackDataExt,
                PMessageIdName = msgIdName,
                MessageIdNumber = 1337,
                PMessage = testMsg,
                QueueLabelCount = 0,
                CmdBufLabelCount = 0,
                ObjectCount = 1,
                PObjects = &obj
            };

            _debugUtils.SubmitDebugUtilsMessage(_instance, DebugUtilsMessageSeverityFlagsEXT.InfoBitExt, DebugUtilsMessageTypeFlagsEXT.GeneralBitExt, &callbackData);
        }
    }
#if DEBUG

    void RegisterDebugMessenger()
    {
        if (_messenger.HasValue)
        {
            return;
        }

        var severity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt |
            DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
            DebugUtilsMessageSeverityFlagsEXT.InfoBitExt |
            DebugUtilsMessageSeverityFlagsEXT.WarningBitExt;

        var type = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
            DebugUtilsMessageTypeFlagsEXT.ValidationBitExt |
            DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
            DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt;

        _debugCallback = new(CreateDebugMessengerCallback(Logger ?? Log.Logger));

        var info = new DebugUtilsMessengerCreateInfoEXT()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = severity,
            MessageType = type,
            PfnUserCallback = _debugCallback.Value
        };

        VulkanTools.Ensure(_debugUtils.CreateDebugUtilsMessenger(_instance, in info, null, out var messenger));
        _messenger = messenger;
    }

    void UnregisterDebugMessenger()
    {
        if (_messenger is null)
        {
            return;
        }

        _debugUtils.DestroyDebugUtilsMessenger(_instance, _messenger.Value, null);
        _messenger = null;
    }
#endif
}
