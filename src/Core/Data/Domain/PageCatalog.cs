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
        private string pageUrl;
        public PageCatalog(BlogPost page, IEnumerable<BlogPost> allPages, PageCatalog parent = null)
        {
            this.page = page;
            children = new List<PageCatalog>();
            IEnumerable<BlogPost> childPages;
            if (page == null)
            {
                childPages = allPages.Where(eml => eml.ParentId == 0 && !eml.Slug.Equals("home"));
            }
            else
            {
                childPages = allPages.Where(eml => eml.ParentId == this.page.Id);
            }
            if (parent == null)
            {
                this.pageUrl = "page";
            }
            else
            {
                this.pageUrl = parent.pageUrl + "/" + this.page.Slug;
            }

            foreach (BlogPost childPage in childPages)
            {
                PageCatalog child = new PageCatalog(childPage, allPages,this);
                this.children.Add(child);
            }
        }


        public IEnumerable<PageCatalog> Children
        {
            get {
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
        public string PageUrl
        {
            get
            {
                return this.pageUrl;
            }
        }

    }
}
