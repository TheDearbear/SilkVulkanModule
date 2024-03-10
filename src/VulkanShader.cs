using Silk.NET.Vulkan;
using Speed.Engine.Render.Shaders;
using System;

namespace SilkVulkanModule;

internal unsafe sealed class VulkanShader : Shader
{
    internal ShaderModule Shader { get; }

    readonly Device _device;
    readonly Vk _vk;

    public VulkanShader(Vk vk, ShaderType type, byte[] shader, string mainFunction)
        : base(mainFunction, type)
    {
        if (shader.Length % 4 != 0)
        {
            throw new ArgumentException("Shader buffer must be 4 byte aligned!", nameof(shader));
        }

        _vk = vk;
        _device = _vk.CurrentDevice!.Value;

        uint[] code = new uint[shader.Length / 4];
        UIntPtr codeSize = new(unchecked((uint)shader.Length));
        System.Buffer.BlockCopy(shader, 0, code, 0, shader.Length);

        fixed (uint* pCode = code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                PCode = pCode,
                CodeSize = codeSize
            };

            VulkanTools.Ensure(_vk.CreateShaderModule(_device, in createInfo, null, out var module));
            Shader = module;
        }
    }

    public override void Dispose()
    {
        _vk.DestroyShaderModule(_device, Shader, null);
    }
}
