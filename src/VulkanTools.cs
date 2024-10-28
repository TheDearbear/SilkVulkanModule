using Serilog;
using Silk.NET.Vulkan;
using Speed.Engine.Logging;
using Speed.Engine.Render.Shaders;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using Speed.Viewer.Render.Backend.Pipelines;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace SilkVulkanModule;

internal static class VulkanTools
{
    public const string UnknownString = "*Unknown*";

    public static void Ensure(Result result,
        [CallerLineNumber] int cLine = -1,
        [CallerFilePath] string cFile = "",
        [CallerArgumentExpression(nameof(result))] string? cExpression = null)
    {
        if (result == Result.Success)
        {
            return;
        }

        void LogWarning(StringBuilder sb, params object?[] param)
        {
            var logger = Log.Logger.ForContextShortName("Vulkan Tools");

            sb.AppendLine()
                .AppendLine(" - Line: {Expression}")
                .Append(" - File: {File}");

            var expression = cExpression ?? UnknownString;
            var file = UnknownString;

            if (!string.IsNullOrEmpty(cFile))
            {
                file = cFile;
                sb.Append(":{Line}");
            }

            var paramList = new List<object?>(param.Length + 3);
            paramList.AddRange(param);
            paramList.Add(expression);
            paramList.Add(file);
            paramList.Add(cLine);

            logger.Warning(sb.ToString(), [.. paramList]);
        }

        if (result == Result.SuboptimalKhr)
        {
            var sb = new StringBuilder("Vulkan call finished with suboptimal result ({Result})");

            LogWarning(sb, result);
            return;
        }

        if (result == Result.Timeout)
        {
            var sb = new StringBuilder("Vulkan call finished with timeout ({Result})");

            LogWarning(sb, result);
            return;
        }

        string errorMsg = "Vulkan call finished with error: " + (cExpression is not null ? cExpression + " => " : string.Empty) + result;

        throw new InvalidOperationException(errorMsg);
    }

    public unsafe static int StringLength(byte* str)
    {
        int i = 0;
        while (*(str + i++) != 0) ;
        return i - 1;
    }

    public unsafe static string ConvertUTF8(byte* str, int length = -1)
    {
        if (length < 0)
        {
            length = StringLength(str);
        }

        return Encoding.UTF8.GetString(str, length);
    }

    public static Format Convert(TextureFormat format)
        => format switch
        {
            TextureFormat.R8_Byte => Format.R8Uint,
            TextureFormat.R16_UShort => Format.R16Uint,
            TextureFormat.R32_UInt => Format.R32Uint,
            TextureFormat.R32_Float => Format.R32Sfloat,
            TextureFormat.RGBA8 => Format.R8G8B8A8Unorm,
            TextureFormat.R16G16_UShort => Format.R16G16B16A16Uint,
            TextureFormat.R32G32B32_UInt => Format.R32G32B32Uint,
            TextureFormat.R32G32B32_Float => Format.R32G32B32Sfloat,
            TextureFormat.R16G16B16A16_Float => Format.R16G16B16A16Sfloat,
            TextureFormat.R32G32B32A32_UInt => Format.R32G32B32A32Uint,
            TextureFormat.R32G32B32A32_Float => Format.R32G32B32A32Sfloat,
            TextureFormat.A2B10G10R10_Float => Format.A2B10G10R10UnormPack32,
            TextureFormat.DXT1 => Format.BC1RgbUnormBlock,
            TextureFormat.DXT3 => Format.BC2UnormBlock,
            TextureFormat.DXT5 => Format.BC3UnormBlock,
            _ => Format.Undefined // Also covers TextureFormat.Unknown
        };

    public static Format ConvertSRGB(TextureFormat format)
        => format switch
        {
            TextureFormat.R8_Byte => Format.R8Srgb,
            TextureFormat.R16_UShort => Format.R16Sfloat,
            TextureFormat.R32_UInt => Format.R32Sfloat,
            TextureFormat.RGBA8 => Format.R8G8B8A8Srgb,
            TextureFormat.R16G16_UShort => Format.R16G16Sfloat,
            TextureFormat.R32G32B32_UInt => Format.R32G32B32Sfloat,
            TextureFormat.R32G32B32A32_UInt => Format.R32G32B32A32Sfloat,
            TextureFormat.DXT1 => Format.BC1RgbSrgbBlock,
            TextureFormat.DXT3 => Format.BC2SrgbBlock,
            TextureFormat.DXT5 => Format.BC3SrgbBlock,
            _ => Convert(format)
        };

    public static ShaderStageFlags Convert(ShaderType type)
        => type switch
        {
            ShaderType.Fragment => ShaderStageFlags.FragmentBit,
            ShaderType.Compute => ShaderStageFlags.ComputeBit,
            ShaderType.Vertex => ShaderStageFlags.VertexBit,
            _ => ShaderStageFlags.All
        };

    public static ImageLayout Convert(TextureLayout layout)
        => layout switch
        {
            TextureLayout.Undefined => ImageLayout.Undefined,
            TextureLayout.General => ImageLayout.General,
            TextureLayout.ColorAttachment => ImageLayout.ColorAttachmentOptimal,
            TextureLayout.DepthStencilAttachment => ImageLayout.DepthStencilAttachmentOptimal,
            TextureLayout.TransferSource => ImageLayout.TransferSrcOptimal,
            TextureLayout.TransferDestination => ImageLayout.TransferDstOptimal,
            TextureLayout.Present => ImageLayout.PresentSrcKhr,
            _ => throw new ArgumentException($"Unable to get native layout ({layout})", nameof(layout))
        };

    public static PipelineStageFlags GetPipelineStageByLayout(ImageLayout layout)
        => layout switch
        {
            ImageLayout.Undefined => PipelineStageFlags.TopOfPipeBit,
            ImageLayout.ColorAttachmentOptimal => PipelineStageFlags.TopOfPipeBit,
            ImageLayout.TransferDstOptimal => PipelineStageFlags.TransferBit,
            ImageLayout.TransferSrcOptimal => PipelineStageFlags.TransferBit,
            ImageLayout.DepthStencilAttachmentOptimal => PipelineStageFlags.EarlyFragmentTestsBit,
            ImageLayout.PresentSrcKhr => PipelineStageFlags.BottomOfPipeBit,
            _ => throw new ArgumentException($"Unsupported image layout ({layout})", nameof(layout))
        };

    public static AccessFlags GetAccessFlags(ImageLayout layout)
        => layout switch
        {
            ImageLayout.Undefined => AccessFlags.None,
            ImageLayout.ColorAttachmentOptimal => AccessFlags.None,
            ImageLayout.TransferDstOptimal => AccessFlags.TransferWriteBit,
            ImageLayout.TransferSrcOptimal => AccessFlags.TransferReadBit,
            ImageLayout.DepthStencilAttachmentOptimal => AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
            ImageLayout.PresentSrcKhr => AccessFlags.None,
            _ => throw new ArgumentException($"Unable to find access flags for texture layout ({layout})", nameof(layout))
        };

    public static AccessFlags GetAccessFlags(BufferUsageType type)
        => type switch
        {
            BufferUsageType.Uniform => AccessFlags.TransferWriteBit | AccessFlags.TransferReadBit | AccessFlags.UniformReadBit,
            BufferUsageType.Storage => AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit,
            BufferUsageType.Vertex => AccessFlags.TransferWriteBit | AccessFlags.VertexAttributeReadBit,
            BufferUsageType.Index => AccessFlags.ShaderWriteBit | AccessFlags.IndexReadBit,
            BufferUsageType.Staging => AccessFlags.TransferReadBit | AccessFlags.TransferWriteBit,
            _ => throw new ArgumentException($"Unable to find access flags for buffer ({type})", nameof(type))
        };

    public static PipelineBindPoint Convert(PipelineType type)
        => type switch
        {
            PipelineType.Graphics => PipelineBindPoint.Graphics,
            PipelineType.Compute => PipelineBindPoint.Compute,
            _ => throw new ArgumentException($"Unable to determine pipeline type ({type})", nameof(type))
        };

    public static CompareOp Convert(DepthComparator comparator)
        => comparator switch
        {
            DepthComparator.Never => CompareOp.Never,
            DepthComparator.Less => CompareOp.Less,
            DepthComparator.LessEqual => CompareOp.LessOrEqual,
            DepthComparator.Greater => CompareOp.Greater,
            DepthComparator.GreaterEqual => CompareOp.GreaterOrEqual,
            DepthComparator.Equal => CompareOp.Equal,
            DepthComparator.NotEqual => CompareOp.NotEqual,
            DepthComparator.Always => CompareOp.Always,
            _ => throw new ArgumentException($"Unable to determine comparator ({comparator})", nameof(comparator))
        };

    public static DescriptorType Convert(DescriptionType type)
        => type switch
        {
            DescriptionType.Sampler => DescriptorType.Sampler,
            DescriptionType.SampledImage => DescriptorType.SampledImage,
            DescriptionType.StorageImage => DescriptorType.StorageImage,
            DescriptionType.Uniform => DescriptorType.UniformBuffer,
            DescriptionType.Storage => DescriptorType.StorageImage,
            DescriptionType.InputAttachment => DescriptorType.InputAttachment,
            _ => throw new ArgumentException("Received unknown description type", nameof(type))
        };

    public static ImageType Convert(TextureType type)
        => type switch
        {
            TextureType.OneDimensional => ImageType.Type1D,
            TextureType.TwoDimensional => ImageType.Type2D,
            _ => throw new ArgumentException("Unable to get texture type", nameof(type))
        };

    public static ImageUsageFlags Convert(TextureUsage usage)
        => ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | usage switch
        {
            TextureUsage.Sampled => ImageUsageFlags.SampledBit,
            TextureUsage.Storage => ImageUsageFlags.StorageBit,
            TextureUsage.ColorAttachment => ImageUsageFlags.ColorAttachmentBit,
            TextureUsage.DepthStencilAttachment => ImageUsageFlags.DepthStencilAttachmentBit,
            TextureUsage.InputAttachment => ImageUsageFlags.InputAttachmentBit,
            _ => throw new ArgumentException("Unable to determine texture usage", nameof(usage))
        };

    public static ImageViewType Convert(ImageType type)
        => type switch
        {
            ImageType.Type1D => ImageViewType.Type1D,
            ImageType.Type2D => ImageViewType.Type2D,
            ImageType.Type3D => ImageViewType.Type3D,
            _ => throw new ArgumentException("Invalid value of image type passed", nameof(type))
        };

    public static ImageAspectFlags Convert(ImageUsageFlags usage)
    {
        if (usage.HasFlag(ImageUsageFlags.SampledBit) || usage.HasFlag(ImageUsageFlags.ColorAttachmentBit))
        {
            return ImageAspectFlags.ColorBit;
        }
        else if (usage.HasFlag(ImageUsageFlags.DepthStencilAttachmentBit))
        {
            return ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit;
        }
        else
        {
            return ImageAspectFlags.None;
        }
    }

    public static ColorSpaceKHR Convert(ColorSpace colorSpace)
        => colorSpace switch
        {
            ColorSpace.SRGB => ColorSpaceKHR.SpaceSrgbNonlinearKhr,
            ColorSpace.ExtendedSRGB => ColorSpaceKHR.SpaceExtendedSrgbLinearExt,
            ColorSpace.DisplayP3 => ColorSpaceKHR.SpaceDisplayP3LinearExt,
            ColorSpace.DCIP3 => ColorSpaceKHR.SpaceDciP3LinearExt,
            ColorSpace.HDR10 => ColorSpaceKHR.SpaceHdr10ST2084Ext,
            _ => throw new ArgumentException("Unable to determine color space", nameof(colorSpace))
        };
}
