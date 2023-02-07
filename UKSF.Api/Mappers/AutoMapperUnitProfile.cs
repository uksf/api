using AutoMapper;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Mappers;

public class AutoMapperUnitProfile : Profile
{
    public AutoMapperUnitProfile()
    {
        CreateMap<DomainUnit, ResponseUnit>();
    }
}
