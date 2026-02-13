using Pms.Dto.categoryDto;
using Pms.Dto.PaginationDto;
using Pms.Service.Interface;
using PmsRepository.Interface;
using PmsRepository.Models;
using Shared.Exceptions;
using System.Security.Claims;

namespace Pms.Service.Service
{
    public class CategoryService : ICategoryService
    {
        private readonly IGenericRepository<Category> _repository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICurrentUserContext _currentUser;
        private readonly IImageService _imageService;
        
        public CategoryService(
            IGenericRepository<Category> repository,
            ICategoryRepository categoryRepository,
            IProductRepository productRepository,
            ICurrentUserContext currentUser,
            IImageService imageService)
        {
            _repository = repository;
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
            _currentUser = currentUser;
            _imageService = imageService;
        }
        public async Task CreateAsync(CategoryCreateDto categoryCreateDto)
        {
            if (string.IsNullOrWhiteSpace(categoryCreateDto.CategoryName))
                throw new InvalidOperationAppException("Category name cannot be empty.");

            bool categoryExists = await _categoryRepository.ExistsAsync(c =>
                c.CategoryName.ToLower() == categoryCreateDto.CategoryName.Trim().ToLower()
                && c.IsActive);

            if (categoryExists)
                throw new AlreadyExistsException("Category already exists.");

            string? imageUrl = null;
            if (categoryCreateDto.CategoryImage != null)
            {
                imageUrl = await _imageService.SaveImageAsync(
                    categoryCreateDto.CategoryImage,
                    "categories"
                );
            }

            var category = new Category
            {
                CategoryName = categoryCreateDto.CategoryName.Trim(),
                CategoryDescription = categoryCreateDto.CategoryDescription,
                CategoryImageUrl = imageUrl,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
                IsActive = true
            };

            await _repository.AddAsync(category);
            await _repository.SaveAsync();

        }

        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _repository.GetByIdAsync(id);

            if (category == null || !category.IsActive)
            {
                throw new NotFoundException("Category does not exist.");
            }

            bool hasProducts = await _productRepository.ExistsAsync(p =>
                p.CategoryId == id );

            if (hasProducts)
            {
                throw new InvalidOperationAppException(
                    "Category cannot be deleted because products are associated with it."
                );
            }

            category.IsActive = false;
            category.UpdatedDate = DateTime.UtcNow;
            category.UpdatedBy = _currentUser.UserId;

            _repository.Update(category);
            await _repository.SaveAsync();

            return true;
        }

        public async Task<(List<CategoryResponseDto>data,Paginationresponse pagination)> GetAllAsync(Categorylistrequest request)
        {
            try
            {
                var categories = await _repository.GetAllAsync();

                if (categories == null)
                {
                    throw new InvalidOperationAppException(
                        "Failed to retrieve categories."
                    );
                }

                var page = request.Pagination?.PageNumber <= 0 ? 1 : request.Pagination.PageNumber;
                var size = request.Pagination?.PageSize <= 0 ? 10 : request.Pagination.PageSize;

                var query = categories.AsQueryable();
                if (!string.IsNullOrEmpty(request.Filter?.Search))
                {
                    query = query.Where(c =>
                        c.CategoryName.Contains(request.Filter.Search,
                        StringComparison.OrdinalIgnoreCase));
                }
                // Filter by CategoryId
                if (request.Filter?.CategoryId.HasValue == true)
                {
                    query = query.Where(c => c.CategoryId == request.Filter.CategoryId.Value);
                }

                // Filter by Active / Inactive
                if (request.Filter?.IsActive.HasValue == true)
                {
                    query = query.Where(c => c.IsActive == request.Filter.IsActive.Value);
                }
                // SORTING
                bool desc = string.Equals(request.Filter?.Order, "desc",
                    StringComparison.OrdinalIgnoreCase);

                query = (request.Filter?.OrderBy ?? "").ToLower() switch
                {
                    "categoryname" => desc ? query.OrderByDescending(c => c.CategoryName)
                                           : query.OrderBy(c => c.CategoryName),

                    "isactive" => desc ? query.OrderByDescending(c => c.IsActive)
                                       : query.OrderBy(c => c.IsActive),

                    _ => desc ? query.OrderByDescending(c => c.CategoryId)
                              : query.OrderBy(c => c.CategoryId)
                };
                var total = query.Count();

                var skip = (page - 1) * size;

                var pageddata = request.Filter?.GetAll == true
                    ? query.ToList()
                    : query.Skip(skip).Take(size).ToList();

                var data = pageddata.Select(c => new CategoryResponseDto
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    IsActive = c.IsActive,
                    CategoryDescription = c.CategoryDescription,
                    CategoryImageUrl = c.CategoryImageUrl
                }).ToList();

                var pagination = new Paginationresponse
                {
                    Total = total,
                    Page = page,
                    Limit = size,
                    TotalPages = (int)Math.Ceiling(total / (double)size)
                };

                return (data, pagination);
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        public async Task<CategoryDetailsDto> GetByIdAsync(int id)
        {
            var category = await _repository.GetByIdAsync(id);

            if (category == null)
            {
                throw new NotFoundException("Category does not exist.");
            }

            //if (!category.IsActive)
            //{
            //    throw new NotFoundException("Category does not exist.");
            //}

            return new CategoryDetailsDto
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                CategoryDescription = category.CategoryDescription,
                CategoryImageUrl = category.CategoryImageUrl,
                IsActive = category.IsActive,
                CreatedDate = category.CreatedDate
            };

        }

        public async Task<bool> UpdateAsync(int id,CategoryUpdateDto categoryUpdateDto)
        {
            if (string.IsNullOrWhiteSpace(categoryUpdateDto.CategoryName))
            {
                throw new InvalidOperationAppException("Category name cannot be empty.");
            }

            
            var existing = await _repository.GetByIdAsync(id);

            if (existing == null)
            {
                throw new NotFoundException("Category does not exist.");
            }

            var newName = categoryUpdateDto.CategoryName.Trim();

            bool duplicateExists = await _categoryRepository.ExistsAsync(c =>
                c.CategoryId != existing.CategoryId &&
                c.CategoryName.ToLower() == newName.ToLower()
               );

            if (duplicateExists)
            {
                throw new AlreadyExistsException(
                    $"Category '{newName}' already exists."
                );
            }

            string? newImageUrl = null;
            string? oldImageUrl = existing.CategoryImageUrl;

            if (categoryUpdateDto.CategoryImage != null)
            {
                // Save new image (validated inside ImageService)
                newImageUrl = await _imageService.SaveImageAsync(
                    categoryUpdateDto.CategoryImage,
                    "categories"
                );

                existing.CategoryImageUrl = newImageUrl;
            }

            existing.CategoryName = newName;
            existing.CategoryDescription = categoryUpdateDto.CategoryDescription;
            existing.IsActive = categoryUpdateDto.IsActive;
            existing.UpdatedDate = DateTime.Now;
            existing.UpdatedBy = _currentUser.UserId;

            try
            {
                _repository.Update(existing);
                await _repository.SaveAsync();

                if (!string.IsNullOrEmpty(newImageUrl) &&
                    !string.IsNullOrEmpty(oldImageUrl))
                {
                    _imageService.DeleteImage(oldImageUrl);
                }

                return true;
            }
            catch
            {
                if (!string.IsNullOrEmpty(newImageUrl))
                    _imageService.DeleteImage(newImageUrl);

                throw;
            }
        }
    }
}
