﻿using AutoMapper;
using AutoMapper.Configuration;
using SimpleInjector;

namespace Wirehome.Core.Services.DependencyInjection
{
    public class MapperProvider
    {
        private readonly Container _container;

        public MapperProvider(Container container)
        {
            _container = container;
        }

        public IMapper GetMapper(string adapterRepository)
        {
            var mce = new MapperConfigurationExpression();
            mce.ConstructServicesUsing(_container.GetInstance);
            mce.AddProfile(new WirehomeMappingProfile(adapterRepository));
            
            var mc = new MapperConfiguration(mce);
            
            return new Mapper(mc, t => _container.GetInstance(t));
        }
    }


}
