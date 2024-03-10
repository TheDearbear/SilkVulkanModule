using Silk.NET.Vulkan;
using Speed.Viewer.Render.Backend;
using System;

namespace SilkVulkanModule;

internal unsafe partial class VulkanTexture
{
    public bool TransitionLayout(VulkanCommandBuffer cmd, TextureLayout newLayout, ImageSubresourceRange range)
    {
        if (!cmd.Recording)
        {
            return false;
        }

        if (Image is null)
        {
            throw new InvalidOperationException("Image is not loaded!");
        }

        if (Layout == newLayout)
        {
            return true;
        }

        var newNativeLayout = VulkanTools.Convert(newLayout);
        var srcStage = VulkanTools.GetPipelineStageByLayout(NativeLayout);
        var dstStage = VulkanTools.GetPipelineStageByLayout(newNativeLayout);

        var imageBarrier = new ImageMemoryBarrier()
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = VulkanTools.GetAccessFlags(NativeLayout),
            DstAccessMask = VulkanTools.GetAccessFlags(newNativeLayout),
            OldLayout = NativeLayout,
            NewLayout = newNativeLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = Image.Value.Item1,
            SubresourceRange = range
        };

        _vk.CmdPipelineBarrier(cmd.CommandBuffer,
            srcStage, dstStage,
            DependencyFlags.ByRegionBit,
            0, null,
            0, null,
            1, &imageBarrier);

        Layout = newLayout;
        NativeLayout = newNativeLayout;

        return true;
    }

    public bool TransitionLayout(VulkanCommandBuffer cmd, uint srcFamily, uint dstFamily, ImageSubresourceRange range)
    {
        if (!cmd.Recording)
        {
            return false;
        }

        if (Image is null)
        {
            throw new InvalidOperationException("Image is not loaded!");
        }

        if (srcFamily == dstFamily)
        {
            return true;
        }

        var stage = VulkanTools.GetPipelineStageByLayout(NativeLayout);
        var accessMask = VulkanTools.GetAccessFlags(NativeLayout);

        var imageBarrier = new ImageMemoryBarrier()
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = accessMask,
            DstAccessMask = accessMask,
            OldLayout = NativeLayout,
            NewLayout = NativeLayout,
            SrcQueueFamilyIndex = srcFamily,
            DstQueueFamilyIndex = dstFamily,
            Image = Image.Value.Item1,
            SubresourceRange = range
        };

        _vk.CmdPipelineBarrier(cmd.CommandBuffer,
            stage, stage,
            DependencyFlags.ByRegionBit,
            0, null,
            0, null,
            1, &imageBarrier);

        return true;
    }

    public bool ForceTransitionLayout(VulkanCommandBuffer cmd, TextureLayout layout, ImageSubresourceRange range)
    {
        if (!cmd.Recording)
        {
            return false;
        }

        if (Image is null)
        {
            throw new InvalidOperationException("Image is not loaded!");
        }

        if (Layout == layout)
        {
            return true;
        }

        var newNativeLayout = VulkanTools.Convert(layout);
        var srcStage = VulkanTools.GetPipelineStageByLayout(ImageLayout.Undefined);
        var dstStage = VulkanTools.GetPipelineStageByLayout(newNativeLayout);

        var imageBarrier = new ImageMemoryBarrier()
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = VulkanTools.GetAccessFlags(ImageLayout.Undefined),
            DstAccessMask = VulkanTools.GetAccessFlags(newNativeLayout),
            OldLayout = ImageLayout.Undefined,
            NewLayout = newNativeLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = Image.Value.Item1,
            SubresourceRange = range
        };

        _vk.CmdPipelineBarrier(cmd.CommandBuffer,
            srcStage, dstStage,
            DependencyFlags.ByRegionBit,
            0, null,
            0, null,
            1, &imageBarrier);

        Layout = layout;
        NativeLayout = newNativeLayout;

        return true;
    }
}
