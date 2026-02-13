using Pms.Dto.categoryDto;
using Pms.Dto.PaginationDto;
using Pms.Dto.ProductDto;
using Pms.Dto.ProductDto.ProductDto;
using Pms.Service.Interface;
using PmsRepository.Interface;
using PmsRepository.Models;
using Shared.Exceptions;
using System.ComponentModel.DataAnnotations;
namespace Pms.Service.Service
{
    public class ProductService : IProductService
    {
        private readonly IGenericRepository<Product> _productRepo;
        private readonly IGenericRepository<Category> _categoryRepo;
        private readonly IProductRepository _productRepository;
        private readonly ICurrentUserContext _currentUser;
        private readonly IImageService _imageService;

        public ProductService(
            IGenericRepository<Product> productRepo,
            IGenericRepository<Category> categoryRepo,
            IProductRepository productRepository,
            ICurrentUserContext currentUser,
            IImageService imageService)
        {
            _productRepo = productRepo;
            _productRepository = productRepository;
            _categoryRepo = categoryRepo;
            _currentUser = currentUser;
            _imageService = imageService;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var product = await _productRepo.GetByIdAsync(id);

            if (product == null || !product.IsActive)
            {
                throw new NotFoundException("Product does not exist.");
            }

            product.IsActive = false;
            product.UpdatedDate = DateTime.UtcNow;
            product.UpdatedBy = _currentUser.UserId;

            _productRepo.Update(product);
            await _productRepo.SaveAsync();

            return true;
        }

        public async Task<(List<ProductResponseDto> data, Paginationresponse pagination)> GetAllAsync(ProductListRequest request)
        {

         
            var products = await _productRepo.GetAllAsync();
            var categories = await _categoryRepo.GetAllAsync();
   

            if (products == null)
            {
                throw new InvalidOperationAppException(
                    "Failed to retrieve products."
                );
            }

            if (categories == null)
            {
                throw new InvalidOperationAppException(
                    "Failed to retrieve categories."
                );
            }
            var page = request.Pagination?.PageNumber <= 0 ? 1 : request.Pagination.PageNumber;
            var size = request.Pagination?.PageSize <= 0 ? 10 : request.Pagination.PageSize;

            var categoryLookup = categories
                //  .Where(c => c.IsActive)
                .ToDictionary(c => c.CategoryId, c => c.CategoryName);

            var query = products.AsQueryable();
            if (request.Filter.CategoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == request.Filter.CategoryId);

            }
            if (request.Filter.FromDate.HasValue)
            {
                query = query.Where(p => p.CreatedDate >= request.Filter.FromDate);
            }
            if (request.Filter.ToDate.HasValue)
            {
                query = query.Where(p => p.CreatedDate <= request.Filter.ToDate);
            }
            if (!string.IsNullOrEmpty(request.Filter.Search))
                query = query.Where(p =>
                    p.ProductName.Contains(request.Filter.Search, StringComparison.OrdinalIgnoreCase) ||
                    categoryLookup[p.CategoryId].Contains(request.Filter.Search, StringComparison.OrdinalIgnoreCase));

            bool desc = string.Equals(request.Filter.Order, "desc", StringComparison.OrdinalIgnoreCase);

            query = (request.Filter.OrderBy ?? "").ToLower() switch
            {
                "productname" => desc ? query.OrderByDescending(p => p.ProductName)
                                      : query.OrderBy(p => p.ProductName),

                "categoryname" => desc ? query.OrderByDescending(p => categoryLookup[p.CategoryId])
                                       : query.OrderBy(p => categoryLookup[p.CategoryId]),

                "price" => desc ? query.OrderByDescending(p => p.Price)
                                : query.OrderBy(p => p.Price),

                _ => desc ? query.OrderByDescending(p => p.UpdatedDate ?? p.CreatedDate)
                          : query.OrderBy(p => p.UpdatedDate ?? p.CreatedDate)
            };


            var total = query.Count();


            var skip = (page - 1) * size;

            var pageddata = request.Filter.GetAll == true ? query.ToList() : query.Skip(skip).Take(size).ToList();



            var data = pageddata.Select(p => new ProductResponseDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryName = categoryLookup[p.CategoryId],
                Price = p.Price,
                ProductImageUrl = p.ProductImageUrl
            }).ToList();
            //var pageddata = filteredproducts
            //    .Skip((request.PageNumber - 1) * request.PageSize)
            //    .Take(request.PageSize)
            //    .Select(p => new ProductResponseDto
            //    {
            //        ProductId = p.ProductId,
            //        ProductName = p.ProductName,
            //        CategoryName = categoryLookup.TryGetValue(
            //            p.CategoryId, out var categoryName)
            //                ? categoryName
            //                : "N/A",
            //        Price = p.Price,
            //        ProductImageUrl = p.ProductImageUrl
            //    }).ToList();

            var pagination = new Paginationresponse
            {
                Total = total,
                Page =page,
                Limit = size,
                TotalPages = (int)Math.Ceiling(total / (double)size)
            };

            return (data, pagination);


        }

        public async Task<ProductDetailsDto> GetByIdAsync(int id)
        {
            var product = await _productRepo.GetByIdAsync(id);

            if (product == null || !product.IsActive)
            {
                throw new NotFoundException("Product does not exist.");
            }

            var categories = await _categoryRepo.GetAllAsync();

            if (categories == null)
            {
                throw new InvalidOperationAppException(
                    "Failed to retrieve categories."
                );
            }

            var category = categories
                .FirstOrDefault(c =>
                    c.CategoryId == product.CategoryId 
                    );

            return new ProductDetailsDto
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Price = product.Price,
                ProductDescription = product.ProductDescription,
                ProductImageUrl = product.ProductImageUrl,
                Sku = product.Sku,
                CategoryName = category?.CategoryName ?? "N/A",
                CreatedDate = product.CreatedDate
            };
        }

        public async Task CreateAsync(ProductCreateDto productCreateDto)
        {
            if (string.IsNullOrWhiteSpace(productCreateDto.ProductName))
            {
                throw new InvalidOperationAppException(
                    "Product name cannot be empty."
                );
            }

            if (productCreateDto.Price <= 0)
            {
                throw new InvalidOperationAppException(
                    "Product price must be greater than zero."
                );
            }

            // 2️⃣ Validate category
            var category = await _categoryRepo.GetByIdAsync(
                productCreateDto.CategoryId);

            if (category == null || !category.IsActive)
            {
                throw new NotFoundException("Invalid category.");
            }

            // 3️⃣ Check duplicate product (same name + category)
            bool productExists = await _productRepository.ExistsAsync(p =>
                p.ProductName.ToLower() ==
                    productCreateDto.ProductName.Trim().ToLower()
                && p.CategoryId == productCreateDto.CategoryId
               );

            if (productExists)
            {
                throw new AlreadyExistsException(
                    "Product already exists in this category."
                );
            }

            var lastSku = await _productRepo.GetLastSkuAsync();
            var newSku = SkuGenerator.GenerateNextSku(lastSku);

            string? imageUrl = null;
            if (productCreateDto.ProductImage != null)
            {
                imageUrl = await _imageService.SaveImageAsync(
                    productCreateDto.ProductImage,
                    "products"
                );
            }
            var product = new Product
            {
                Sku = newSku,
                ProductName = productCreateDto.ProductName.Trim(),
                CategoryId = productCreateDto.CategoryId,
                ProductDescription = productCreateDto.ProductDescription,
                ProductImageUrl = imageUrl,
                Price = productCreateDto.Price,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
                IsActive = true
            };

            await _productRepo.AddAsync(product);
            await _productRepo.SaveAsync();
        }

        public async Task<bool> UpdateAsync(int id, ProductUpdateDto product)
        {
            if (string.IsNullOrWhiteSpace(product.ProductName))
            {
                throw new InvalidOperationAppException(
                    "Product name cannot be empty."
                );
            }

            if (product.Price <= 0)
            {
                throw new InvalidOperationAppException(
                    "Product price must be greater than zero."
                );
            }

            var existing = await _productRepo.GetByIdAsync(id);

            if (existing == null || !existing.IsActive)
            {
                throw new NotFoundException("Product does not exist.");
            }

            var category = await _categoryRepo.GetByIdAsync(product.CategoryId);

            if (category == null || !category.IsActive)
            {
                throw new NotFoundException("Invalid category.");
            }

            var newName = product.ProductName.Trim();

            bool duplicateExists = await _productRepository.ExistsAsync(p =>
                p.ProductId != existing.ProductId &&
                p.ProductName.ToLower() == newName.ToLower() &&
                p.CategoryId == product.CategoryId&&
                 p.IsActive);

            if (duplicateExists)
            {
                throw new AlreadyExistsException(
                    "Another product with the same name already exists in this category."
                );
            }
            string? newImageUrl = null;
            string? oldImageUrl = existing.ProductImageUrl;

            if (product.ProductImage != null)
            {
                // Save new image (validated inside ImageService)
                newImageUrl = await _imageService.SaveImageAsync(
                    product.ProductImage,
                    "products"
                );

                existing.ProductImageUrl = newImageUrl;
            }
            existing.ProductName = newName;
            existing.CategoryId = product.CategoryId;
            existing.ProductDescription = product.ProductDescription;
            existing.Price = product.Price;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;

            try
            {
                _productRepo.Update(existing);
                await _productRepo.SaveAsync();

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

        public async Task<string> GetNextSkuAsync()
        {
            var lastSku = await _productRepository.GetLastSkuAsync();

            return SkuGenerator.GenerateNextSku(lastSku);
        }

        public async Task<IEnumerable<ProductResponseDto>> GetByCategoryIdAsync(int categoryId)
        {
            var category = await _categoryRepo.GetByIdAsync(categoryId);

            if (category == null || !category.IsActive)
            {
                throw new NotFoundException("Category does not exist.");
            }

            var products = await _productRepository
                .GetByCategoryIdAsync(categoryId);

            if (!products.Any())
            {
                throw new NotFoundException("No products found in this category.");
            }

            return products.Select(p => new ProductResponseDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                CategoryName = category.CategoryName,
                Price = p.Price
            }).ToList();
        }

       
    }
}
