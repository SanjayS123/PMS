using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pms.Dto.PaginationDto
{
    public  class PaginationRequest
    {

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        public string? Search { get; set; }
    }

    public class Paginationresponse 
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
        public int TotalPages { get; set; }
    }
    public class Categorylistrequest
    {
        public PaginationRequest Pagination { get; set; }
        public categoryfilter Filter { get; set; } = new categoryfilter();
    }


    public class categoryfilter
    {
        public bool? GetAll { get; set; } = false;
        public int? CategoryId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public string? Search { get; set; }
        public string? OrderBy { get; set; }   // productname / price / categoryname
        public string? Order { get; set; }     // asc / desc
    }

}
