using AutoMapper;
using UKSF.Api.Models.Units;

namespace UKSF.Api.AppStart {
    public class AutoMapperConfigurationProfile : Profile {
        public AutoMapperConfigurationProfile() {
            CreateMap<Unit, ResponseUnit>();
        }
    }
}
