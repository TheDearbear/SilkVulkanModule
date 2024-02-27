using Serilog;
using Silk.NET.Vulkan;
using Speed.Engine.Render.Shaders;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace VulkanModule;

internal static class VulkanTools
{
	public static void Ensure(Result result,
		[CallerLineNumber] int cLine = -1,
		[CallerFilePath] string cFile = "",
		[CallerArgumentExpression(nameof(result))] string? cExpression = null)
	{
		if (result == Result.Success)
		{
			return;
		}

		if (result == Result.SuboptimalKhr)
		{
			var sb = new StringBuilder($"Vulkan call finished with suboptimal result ({result})");
			sb.AppendLine(" - Line: " + (cExpression ?? "*Unknown*"));
			sb.Append(" - File: " + (cFile != string.Empty ? $"{cFile}:{cLine}" : "*Unknown*"));

			Log.Warning(sb.ToString());
			return;
		}

		if (result == Result.Timeout)
		{
			var sb = new StringBuilder($"Vulkan call finished with timeout ({result})");
			sb.AppendLine(" - Line: " + (cExpression ?? "*Unknown*"));
			sb.Append(" - File: " + (cFile != string.Empty ? $"{cFile}:{cLine}" : "*Unknown*"));

			Log.Warning(sb.ToString());
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
			TextureFormat.R32G32B32A32_UInt => Format.R32G32B32A32Uint,
			TextureFormat.R32G32B32A32_Float => Format.R32G32B32A32Sfloat,
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
			_ => throw new ArgumentException($"Unable to get native layout ({layout})")
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
			_ => throw new ArgumentException($"Unsupported image layout ({layout})")
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
			_ => throw new ArgumentException($"Unable to find access flags for texture layout ({layout})")
		};

	public static AccessFlags GetAccessFlags(BufferUsageType type)
		=> type switch
		{
			BufferUsageType.Uniform => AccessFlags.TransferWriteBit | AccessFlags.TransferReadBit | AccessFlags.UniformReadBit,
			BufferUsageType.Storage => AccessFlags.ShaderReadBit | AccessFlags.ShaderWriteBit,
			BufferUsageType.Vertex => AccessFlags.TransferWriteBit | AccessFlags.VertexAttributeReadBit,
			BufferUsageType.Index => AccessFlags.ShaderWriteBit | AccessFlags.IndexReadBit,
			BufferUsageType.Staging => AccessFlags.TransferReadBit | AccessFlags.TransferWriteBit,
			_ => throw new ArgumentException($"Unable to find access flags for buffer ({type})")
		};
}
