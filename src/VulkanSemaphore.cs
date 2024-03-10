using Silk.NET.Vulkan;
using System;

using SpeedSemaphore = Speed.Viewer.Render.Backend.Semaphore;

using VkSemaphore = Silk.NET.Vulkan.Semaphore;

namespace SilkVulkanModule;

internal unsafe sealed class VulkanSemaphore : SpeedSemaphore
{
    ulong _cachedValue;
    public override ulong Value
    {
        get
        {
            fixed (ulong* pValue = &_cachedValue)
            {
                VulkanTools.Ensure(_vk.GetSemaphoreCounterValue(_device, Semaphore, pValue));
            }
            return _cachedValue;
        }

        set
        {
            var signal = new SemaphoreSignalInfo()
            {
                SType = StructureType.SemaphoreSignalInfo,
                Semaphore = Semaphore,
                Value = value
            };

            VulkanTools.Ensure(_vk.SignalSemaphore(_device, in signal));
            _cachedValue = value;
        }
    }

    internal VkSemaphore Semaphore { get; }

    readonly Device _device;
    readonly Vk _vk;

    public VulkanSemaphore(Vk vk)
    {
        _vk = vk;
        _device = _vk.CurrentDevice!.Value;

        var typeCreateInfo = new SemaphoreTypeCreateInfo()
        {
            SType = StructureType.SemaphoreTypeCreateInfo,
            SemaphoreType = SemaphoreType.Timeline
        };
        var createInfo = new SemaphoreCreateInfo()
        {
            SType = StructureType.SemaphoreCreateInfo,
            PNext = &typeCreateInfo
        };

        VulkanTools.Ensure(_vk.CreateSemaphore(_device, in createInfo, null, out var semaphore));
        Semaphore = semaphore;
    }

    public override ulong? Wait(TimeSpan timeout)
    {
        var semaphore = Semaphore;
        var value = 0ul;

        var info = new SemaphoreWaitInfo()
        {
            SType = StructureType.SemaphoreWaitInfo,
            SemaphoreCount = 1,
            PSemaphores = &semaphore,
            PValues = &value
        };

        Result result;
        VulkanTools.Ensure(result = _vk.WaitSemaphores(_device, in info, unchecked((ulong)(timeout.TotalMilliseconds * 1_000_000))));

        return result == Result.Success ?
            _cachedValue = value :
            null;
    }

    public override void Dispose()
    {
        _vk.DestroySemaphore(_device, Semaphore, null);
    }
}
