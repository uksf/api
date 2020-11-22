using AutoMapper;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Mappers {
    public class AutoMapperUnitProfile : Profile {
        public AutoMapperUnitProfile() {
            CreateMap<Unit, ResponseUnit>();
        }
    }
}
