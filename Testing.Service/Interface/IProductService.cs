using Pms.Dto.PaginationDto;
using Pms.Dto.ProductDto;
using Pms.Dto.ProductDto.ProductDto;

namespace Pms.Service.Interface
{
    public  interface IProductService
    {
        Task<(List<ProductResponseDto> data, Paginationresponse pagination)> GetAllAsync(ProductListRequest request);
        Task<ProductDetailsDto> GetByIdAsync(int id);
        Task CreateAsync(ProductCreateDto product);
        Task<bool> UpdateAsync(int id,ProductUpdateDto product);
        Task<bool> DeleteAsync(int id);
        Task<string> GetNextSkuAsync();
        Task<IEnumerable<ProductResponseDto>> GetByCategoryIdAsync(int categoryId);

    }
}
