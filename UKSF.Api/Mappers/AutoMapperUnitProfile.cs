using AutoMapper;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Mappers;

public class AutoMapperUnitProfile : Profile
{
    public AutoMapperUnitProfile()
    {
        CreateMap<DomainUnit, ResponseUnit>();
    }
}
