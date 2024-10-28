using Silk.NET.Vulkan;
using Speed.Engine.Textures;
using Speed.Viewer.Render.Backend;
using System;

namespace SilkVulkanModule;

internal unsafe sealed partial class VulkanTexture : Texture
{
    internal ValueTuple<Image, ImageView>? Image { get; private set; }
    internal ImageLayout NativeLayout { get; set; }
    internal TextureLayout Layout { get; set; }
    internal TextureUsage Usage { get; set; }

    readonly int _samples;
    readonly TextureTiling _tiling;

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

        if (info.Samples < 0 || info.Samples > 0x7F)
        {
            throw new ArgumentException("Invalid number of samples", nameof(info));
        }

        _samples = info.Samples;
        _tiling = info.Tiling;
        Usage = info.Usage;
        Layout = info.Layout;
        NativeLayout = VulkanTools.Convert(Layout);
    }

    public VulkanTexture(Vk vk, Image image, ImageView view, TextureInfo info)
        : this(vk, info)
    {
        Image = new(image, view);
    }

    public ValueTuple<Image, ImageView> CreateImage()
    {
        if (Image.HasValue)
        {
            return Image.Value;
        }

        var createInfo = new ImageCreateInfo()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = VulkanTools.Convert(Type),
            Format = VulkanTools.Convert(Format),
            Extent = new(unchecked((uint)Width), unchecked((uint)Height)),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = (SampleCountFlags)_samples,
            Tiling = _tiling == TextureTiling.Optimal ? ImageTiling.Optimal : ImageTiling.Linear,
            Usage = VulkanTools.Convert(Usage),
            SharingMode = SharingMode.Exclusive,
            InitialLayout = NativeLayout
        };

        VulkanTools.Ensure(_vk.CreateImage(_device, in createInfo, null, out var image));

        var viewCreateInfo = new ImageViewCreateInfo()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = VulkanTools.Convert(createInfo.ImageType),
            Format = createInfo.Format,
            Components = new(ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity, ComponentSwizzle.Identity),
            SubresourceRange = new(VulkanTools.Convert(createInfo.Usage), 0, Vk.RemainingMipLevels, 0, Vk.RemainingArrayLayers)
        };

        VulkanTools.Ensure(_vk.CreateImageView(_device, in viewCreateInfo, null, out var view));

        return new(image, view);
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
