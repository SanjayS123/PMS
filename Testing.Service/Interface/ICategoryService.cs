using Pms.Dto.categoryDto;
using Pms.Dto.PaginationDto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pms.Service.Interface
{
    public interface ICategoryService
    {
        Task<(List<CategoryResponseDto> data, Paginationresponse pagination)> GetAllAsync(Categorylistrequest request);
        Task<CategoryDetailsDto> GetByIdAsync(int id);
        Task CreateAsync(CategoryCreateDto categoryCreateDto);
        Task<bool> UpdateAsync(int id,CategoryUpdateDto categoryUpdateDto);
        Task<bool> DeleteAsync(int id);
    }
}
