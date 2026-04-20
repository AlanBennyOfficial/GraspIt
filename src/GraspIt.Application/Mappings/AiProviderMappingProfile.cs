using AutoMapper;
using GraspIt.Core.Entities;
using GraspIt.Application.DTOs;

namespace GraspIt.Application.Mappings;

public class AiProviderMappingProfile : Profile
{
    public AiProviderMappingProfile()
    {
        CreateMap<AiProvider, AiProviderDto>().ReverseMap();
    }
}
