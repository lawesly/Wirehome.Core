﻿using System;
using Windows.Devices.I2c;
using Wirehome.Contracts.Core;

namespace Wirehome.Raspberry
{

    public class RaspberryI2cDevice : INativeI2cDevice
    {
        private readonly I2cDevice _i2CDevice;
          
        public RaspberryI2cDevice(I2cDevice i2CDevice)
        {
            _i2CDevice = i2CDevice ?? throw new ArgumentNullException(nameof(i2CDevice));
        }
        
        public void Dispose()
        {
            if (_i2CDevice != null)
            {
                _i2CDevice.Dispose();
            }
        }
        
        public NativeI2cTransferResult WritePartial(byte[] buffer)
        {
            var result = _i2CDevice.WritePartial(buffer);
            return new NativeI2cTransferResult { BytesTransferred = result.BytesTransferred, Status = (NativeI2cTransferStatus)result.Status };
        }

        public NativeI2cTransferResult ReadPartial(byte[] buffer)
        {
            var result = _i2CDevice.ReadPartial(buffer);
            return new NativeI2cTransferResult { BytesTransferred = result.BytesTransferred, Status = (NativeI2cTransferStatus)result.Status };
        }

        public NativeI2cTransferResult WriteReadPartial(byte[] writeBuffer, byte[] readBuffer)
        {
            var result = _i2CDevice.WriteReadPartial(writeBuffer, readBuffer);
            return new NativeI2cTransferResult { BytesTransferred = result.BytesTransferred, Status = (NativeI2cTransferStatus)result.Status };
        }
    }
}
