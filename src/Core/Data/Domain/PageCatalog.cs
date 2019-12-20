using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Core.Data.Models
{
    public class PageCatalog
    {
        private List<PageCatalog> children;
        private BlogPost page;
        public PageCatalog(BlogPost page, IEnumerable<BlogPost> allPages)
        {
            this.page = page;
            children = new List<PageCatalog>();
            IEnumerable<BlogPost> childPages;
            if (page == null)
            {
                childPages = allPages.Where(eml => eml.ParentId == 0);
            }
            else
            {
                childPages = allPages.Where(eml => eml.ParentId == this.page.Id);
            }
            foreach (BlogPost childPage in childPages)
            {
                PageCatalog child = new PageCatalog(childPage, allPages);
                this.children.Add(child);
            }
        }
       
       
        public IEnumerable<PageCatalog> Children
        {
            get{
                return children;
            }
        }
        public BlogPost Page
        {
            get
            {
                return page;
            }         
        }
        public int NodeID
        {
            get
            {
                return this.page == null ? 0 : this.page.Id;
            }
        }
    }
}
