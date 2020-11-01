using AutoMapper;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.AppStart {
    public class AutoMapperConfigurationProfile : Profile {
        public AutoMapperConfigurationProfile() {
            CreateMap<Unit, ResponseUnit>();
        }
    }
}
