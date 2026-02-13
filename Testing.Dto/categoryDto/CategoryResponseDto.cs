using Microsoft.AspNetCore.Http;
using Pms.Dto.PaginationDto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pms.Dto.categoryDto
{
    public class CategoryResponseDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string? CategoryDescription { get; set; }
        public string? CategoryImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    public class ProductListRequest
    {
        public PaginationRequest Pagination { get; set; }
        public Productfilter Filter { get; set; } = new Productfilter();
    }


    public class Productfilter
    {
        public bool? GetAll { get; set; } = false;
        public int? CategoryId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? IsActive { get; set; }
        public string? Search { get; set; }
        public string? OrderBy { get; set; }  
        public string? Order { get; set; }    
    }
}
