﻿using Silk.NET.Vulkan;
using Speed.Viewer.Render.Backend;
using Speed.Viewer.Render.Backend.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace VulkanModule;

internal partial class VulkanPipeline
{
    static unsafe PipelineVertexInputStateCreateInfo GetVertexInputState(PipelineInfo info, IList<GCHandle> handles)
    {
        var bindingDescriptions = info.Vertex.Bindings.Select(x => new VertexInputBindingDescription()
        {
            Binding = unchecked((uint)x.BindSlot),
            InputRate = (VertexInputRate)x.InputRate,
            Stride = unchecked((uint)x.Stride)
        }).ToArray();

        var attributeDescriptions = info.Vertex.Attributes.Select(x => new VertexInputAttributeDescription()
        {
            Location = unchecked((uint)x.InShaderVertexLocation),
            Binding = unchecked((uint)x.BindSlot),
            Format = VulkanTools.Convert(x.VertexFormat),
            Offset = unchecked((uint)x.Offset)
        }).ToArray();

        void* pVertexBindingDescriptions = null;
        void* pVertexAttributeDescriptions = null;
        if (bindingDescriptions.Length > 0)
        {
            var handle = GCHandle.Alloc(bindingDescriptions, GCHandleType.Pinned);
            handles.Add(handle);
            pVertexBindingDescriptions = handle.AddrOfPinnedObject().ToPointer();
        }
        if (attributeDescriptions.Length > 0)
        {
            var handle = GCHandle.Alloc(attributeDescriptions, GCHandleType.Pinned); ;
            handles.Add(handle);
            pVertexAttributeDescriptions = handle.AddrOfPinnedObject().ToPointer();
        }

        return new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = unchecked((uint)info.Vertex.Bindings.Length),
            PVertexBindingDescriptions = (VertexInputBindingDescription*)pVertexBindingDescriptions,
            VertexAttributeDescriptionCount = unchecked((uint)info.Vertex.Attributes.Length),
            PVertexAttributeDescriptions = (VertexInputAttributeDescription*)pVertexAttributeDescriptions
        };
    }

    static PipelineInputAssemblyStateCreateInfo GetInputAssemblyState()
    {
        return new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList
        };
    }

    static PipelineTessellationStateCreateInfo GetTessellationState()
    {
        return new()
        {
            SType = StructureType.PipelineTessellationStateCreateInfo,
            PatchControlPoints = 1
        };
    }

    static unsafe PipelineViewportStateCreateInfo GetViewportState(PipelineInfo info, out GCHandle handle1, out GCHandle handle2)
    {
        Viewport viewport = new(0, 0, info.Viewport.Width, info.Viewport.Height, 0, 1);
        Rect2D scissor = new(new(), new(unchecked((uint)info.Viewport.Width), unchecked((uint)info.Viewport.Height)));

        handle1 = GCHandle.Alloc(viewport, GCHandleType.Pinned);
        handle2 = GCHandle.Alloc(scissor, GCHandleType.Pinned);

        return new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = (Viewport*)handle1.AddrOfPinnedObject().ToPointer(),
            ScissorCount = 1,
            PScissors = (Rect2D*)handle2.AddrOfPinnedObject().ToPointer()
        };
    }

    static unsafe PipelineRasterizationStateCreateInfo GetRasterizationState()
    {
        return new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PolygonMode = PolygonMode.Fill,
            CullMode = CullModeFlags.None,
            LineWidth = 1
        };
    }

    static PipelineMultisampleStateCreateInfo GetMultisampleState(PipelineInfo info)
    {
        if (info.Sampling.Samples < 0 || info.Sampling.Samples > 0x7F)
        {
            throw new ArgumentException("Number of pipeline's samples is too big/small", nameof(info));
        }

        return new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = (SampleCountFlags)info.Sampling.Samples
        };
    }

    static PipelineDepthStencilStateCreateInfo GetDepthStencilState(PipelineInfo info)
    {
        if (info.Depth.MinDepth > info.Depth.MaxDepth)
        {
            throw new ArgumentException("Minimum depth value is bigger than maximum depth value", nameof(info));
        }

        return new()
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = info.Depth.EnableDepthTest,
            MinDepthBounds = info.Depth.MinDepth,
            MaxDepthBounds = info.Depth.MaxDepth,
            DepthCompareOp = info.Depth.Comparator switch
            {
                DepthComparator.Never => CompareOp.Never,
                DepthComparator.Less => CompareOp.Less,
                DepthComparator.LessEqual => CompareOp.LessOrEqual,
                DepthComparator.Greater => CompareOp.Greater,
                DepthComparator.GreaterEqual => CompareOp.GreaterOrEqual,
                DepthComparator.Equal => CompareOp.Equal,
                DepthComparator.NotEqual => CompareOp.NotEqual,
                _ => CompareOp.Always
            }
        };
    }

    static unsafe PipelineColorBlendStateCreateInfo GetColorBlendState(PipelineInfo info, out GCHandle handle1)
    {
        var colorBlend = new PipelineColorBlendAttachmentState[info.Color.ColorAttachmentsCount];
        for (int i = 0; i < info.Color.ColorAttachmentsCount; i++)
        {
            colorBlend[i].ColorWriteMask |=
                ColorComponentFlags.RBit |
                ColorComponentFlags.GBit |
                ColorComponentFlags.BBit |
                ColorComponentFlags.ABit;
        }

        handle1 = GCHandle.Alloc(colorBlend, GCHandleType.Pinned);

        return new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = unchecked((uint)info.Color.ColorAttachmentsCount),
            PAttachments = (PipelineColorBlendAttachmentState*)handle1.AddrOfPinnedObject().ToPointer()
        };
    }

    static unsafe PipelineDynamicStateCreateInfo GetDynamicState()
    {
        var states = new DynamicState[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        };

        fixed (DynamicState* pStates = states)
        {
            return new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = unchecked((uint)states.Length),
                PDynamicStates = pStates
            };
        }
    }
}
