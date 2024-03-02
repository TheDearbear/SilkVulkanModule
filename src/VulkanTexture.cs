using Silk.NET.Vulkan;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;

namespace VulkanModule;

internal unsafe partial class VulkanTexture : Texture
{
    internal ValueTuple<Image, ImageView>? Image { get; private set; }
    internal ImageLayout NativeLayout { get; set; }
	internal TextureLayout Layout { get; set; }

	readonly int _samples;
	readonly TextureTiling _tiling;
	readonly TextureUsage _usage;

    readonly Device _device;
    readonly Vk _vk;

    public VulkanTexture(Vk vk, TextureInfo info)
    {
        _vk = vk;
        _device = _vk.CurrentDevice!.Value;
        Width = info.Width;
        Height = info.Height;
        Mipmaps = info.MipLevels;
        Format = info.Format;

        _samples = info.Samples;
        _tiling = info.Tiling;
        _usage = info.Usage;
        Layout = info.Layout;
        NativeLayout = VulkanTools.Convert(Layout);
    }

    public VulkanTexture(Vk vk, Image image, ImageView view, TextureInfo info)
        : this(vk, info)
    {
        Image = new(image, view);
    }

    public override void Dispose()
    {
        if (Image.HasValue)
        {
            _vk.DestroyImageView(_device, Image.Value.Item2, null);
            _vk.DestroyImage(_device, Image.Value.Item1, null);
            Image = null;
        }
    }

    public override void LoadTexture(byte[] data)
    {
        throw new NotImplementedException();
    }
}
