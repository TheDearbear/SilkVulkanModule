using Silk.NET.Vulkan;
using Speed.Engine.Render.Shaders;
using Speed.Viewer.Render.Backend.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using SpeedPipeline = Speed.Viewer.Render.Backend.Pipeline;
using SpeedRenderPass = Speed.Viewer.Render.Backend.RenderPass;

using VkPipeline = Silk.NET.Vulkan.Pipeline;

namespace VulkanModule;

internal unsafe sealed partial class VulkanPipeline : SpeedPipeline
{
    public VkPipeline Pipeline { get; }

    internal DescriptorSetLayout[] SetLayouts { get; }

    readonly PhysicalDeviceFeatures _features;
    readonly Device _device;
    readonly Vk _vk;

    public VulkanPipeline(Vk vk, PhysicalDeviceFeatures features, PipelineInfo info, SpeedRenderPass renderPass)
        : base(info)
    {
        _vk = vk;
        _features = features;
        _device = _vk.CurrentDevice!.Value;

        if (renderPass is not VulkanRenderPass vkRenderPass)
        {
            throw new ArgumentException("Render pass belongs to different backend", nameof(renderPass));
        }

        IEnumerable<VulkanShader> vkShaders = Shaders.OfType<VulkanShader>();
        if (vkShaders.Count() != Shaders.Length)
        {
            throw new ArgumentException("Some of the shaders belong to different backend", nameof(info));
        }

        SetLayouts = CreateSetLayouts(info);
        PipelineLayout layout = CreateLayout(info, SetLayouts);
        VkPipeline pipeline;
        if (info.Type == PipelineType.Graphics)
        {
            var vertex = vkShaders.First(x => x.Type == ShaderType.Vertex);
            var fragment = vkShaders.First(x => x.Type == ShaderType.Fragment);

            var shaders = new[] { vertex, fragment };
            List<GCHandle> handles = new(shaders.Length + 6);

            var vertexCreateInfo = GetVertexInputState(info, handles);
            var inputAssemblyCreateInfo = GetInputAssemblyState();
            var tessellationCreateInfo = GetTessellationState();
            var viewportCreateInfo = GetViewportState(info, handles);
            var rasterizationCreateInfo = GetRasterizationState();
            var multisampleCreateInfo = GetMultisampleState(info);
            var depthStencilCreateInfo = GetDepthStencilState(info);
            var colorBlendCreateInfo = GetColorBlendState(info, handles);
            var dynamicCreateInfo = GetDynamicState(handles);

            fixed (PipelineShaderStageCreateInfo* pStages = shaders.Select(x =>
            {
                var handle = GCHandle.Alloc(Encoding.UTF8.GetBytes(x.FunctionName + '\0'), GCHandleType.Pinned);
                handles.Add(handle);

                return new PipelineShaderStageCreateInfo()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = VulkanTools.Convert(x.Type),
                    PName = (byte*)handle.AddrOfPinnedObject().ToPointer(),
                    Module = x.Shader
                };
            }).ToArray())
            {
                var createInfo = new GraphicsPipelineCreateInfo()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    Flags = PipelineCreateFlags.None,
                    StageCount = unchecked((uint)shaders.Length),
                    PStages = pStages,
                    PVertexInputState = &vertexCreateInfo,
                    PInputAssemblyState = &inputAssemblyCreateInfo,
                    PTessellationState = &tessellationCreateInfo,
                    PViewportState = &viewportCreateInfo,
                    PRasterizationState = &rasterizationCreateInfo,
                    PMultisampleState = &multisampleCreateInfo,
                    PDepthStencilState = &depthStencilCreateInfo,
                    PColorBlendState = &colorBlendCreateInfo,
                    PDynamicState = &dynamicCreateInfo,
                    Layout = layout,
                    RenderPass = vkRenderPass.RenderPass,
                    Subpass = 0
                };

                VulkanTools.Ensure(_vk.CreateGraphicsPipelines(_device, new(), 1, in createInfo, null, out pipeline));
            }

            foreach (var handle in handles)
            {
                handle.Free();
            }
        }
        else
        {
            VulkanShader compute = vkShaders.First(x => x.Type == ShaderType.Compute);

            fixed (byte* pName = Encoding.UTF8.GetBytes(compute.FunctionName + '\0'))
            {
                var createInfo = new ComputePipelineCreateInfo()
                {
                    SType = StructureType.ComputePipelineCreateInfo,
                    Flags = PipelineCreateFlags.None,
                    Stage = new()
                    {
                        SType = StructureType.PipelineShaderStageCreateInfo,
                        Stage = VulkanTools.Convert(compute.Type),
                        PName = pName,
                        Module = compute.Shader
                    },
                    Layout = layout
                };

                VulkanTools.Ensure(_vk.CreateComputePipelines(_device, new(), 1, in createInfo, null, out pipeline));
            }
        }

        _vk.DestroyPipelineLayout(_device, layout, null);

        Pipeline = pipeline;
    }

    public override void Dispose()
    {
        _vk.DestroyPipeline(_device, Pipeline, null);

        foreach (var layout in SetLayouts)
        {
            _vk.DestroyDescriptorSetLayout(_device, layout, null);
        }
    }

    DescriptorSetLayout[] CreateSetLayouts(PipelineInfo info)
    {
        return info.Bindings.Select(x =>
        {
            fixed (DescriptorSetLayoutBinding* pBindings = x.Select(x => new DescriptorSetLayoutBinding()
            {
                Binding = unchecked((uint)x.Binding),
                StageFlags = VulkanTools.Convert(x.Stage),
                DescriptorType = VulkanTools.Convert(x.Type),
                DescriptorCount = unchecked((uint)x.Count)
            }).ToArray())
            {
                DescriptorSetLayoutCreateInfo createInfo = new()
                {
                    SType = StructureType.DescriptorSetLayoutCreateInfo,
                    BindingCount = unchecked((uint)x.Length),
                    PBindings = pBindings
                };

                VulkanTools.Ensure(_vk.CreateDescriptorSetLayout(_device, in createInfo, null, out var setLayout));
                return setLayout;
            }
        }).ToArray();
    }

    PipelineLayout CreateLayout(PipelineInfo info, DescriptorSetLayout[] setLayouts)
    {
        fixed (DescriptorSetLayout* pSetLayouts = setLayouts)
        {
            PipelineLayoutCreateInfo createInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo
            };

            if (setLayouts.Length > 0)
            {
                createInfo.SetLayoutCount = unchecked((uint)setLayouts.Length);
                createInfo.PSetLayouts = pSetLayouts;
            }

            VulkanTools.Ensure(_vk.CreatePipelineLayout(_device, in createInfo, null, out var layout));

            return layout;
        }
    }
}
