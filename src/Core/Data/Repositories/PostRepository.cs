﻿using Core.Data.Models;
using Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Data
{
    public interface IPostRepository : IRepository<BlogPost>
    {
        Task<IEnumerable<PostItem>> GetList(Expression<Func<BlogPost, bool>> predicate, Pager pager);
        Task<IEnumerable<PostItem>> GetList(Pager pager, int author = 0, string category = "", string include = "", bool sanitize = false);
        Task<IEnumerable<PostItem>> GetPopular(Pager pager, int author = 0);
        Task<IEnumerable<PostItem>> Search(Pager pager, string term, int author = 0, string include = "", bool sanitize = false, int currentPageId=0);
        Task<PostItem> GetItem(Expression<Func<BlogPost, bool>> predicate, bool sanitize = false);
        Task<PostModel> GetModel(string slug);
        Task<PostModel> GetModelByPostId(int postId);
        Task<PostItem> SaveItem(PostItem item);
        Task SaveCover(int postId, string asset);
        Task<IEnumerable<CategoryItem>> Categories();
        PageCatalog GetPageCatalog();
    }

    public class PostRepository : Repository<BlogPost>, IPostRepository
    {
        AppDbContext _db;

        public PostRepository(AppDbContext db) : base(db)
        {
            _db = db;
        }

        public async Task<IEnumerable<PostItem>> GetList(Expression<Func<BlogPost, bool>> predicate, Pager pager)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;

            var drafts = _db.BlogPosts
                .Where(p => p.Published == DateTime.MinValue).Where(predicate)
                .OrderByDescending(p => p.Published).ToList();

            var pubs = _db.BlogPosts
                .Where(p => p.Published > DateTime.MinValue).Where(predicate)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Published).ToList();

            var items = drafts.Concat(pubs).ToList();
            pager.Configure(items.Count);

            var postPage = items.Skip(skip).Take(pager.ItemsPerPage).ToList();

            return await Task.FromResult(PostListToItems(postPage));
        }

        public async Task<IEnumerable<PostItem>> GetList(Pager pager, int author = 0, string category = "", string include = "", bool sanitize = true)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;

            var posts = new List<BlogPost>();
            foreach (var p in GetPosts(include, author))
            {
                if (string.IsNullOrEmpty(category))
                {
                    posts.Add(p);
                }
                else
                {
                    if (!string.IsNullOrEmpty(p.Categories))
                    {
                        var cats = p.Categories.ToLower().Split(',');
                        if (cats.Contains(category.ToLower()))
                        {
                            posts.Add(p);
                        }
                    }
                }
            }
            pager.Configure(posts.Count);

            var items = new List<PostItem>();
            foreach (var p in posts.Skip(skip).Take(pager.ItemsPerPage).ToList())
            {
                items.Add(PostToItem(p, sanitize));
            }
            return await Task.FromResult(items);
        }

        public async Task<IEnumerable<PostItem>> GetPopular(Pager pager, int author = 0)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;

            var posts = new List<BlogPost>();

            if(author > 0)
            {
                posts = _db.BlogPosts.Where(p => p.Published > DateTime.MinValue && p.AuthorId == author)
                    .OrderByDescending(p => p.PostViews).ThenByDescending(p => p.Published).ToList();
            }
            else
            {
                posts = _db.BlogPosts.Where(p => p.Published > DateTime.MinValue)
                    .OrderByDescending(p => p.PostViews).ThenByDescending(p => p.Published).ToList();
            }

            pager.Configure(posts.Count);

            var items = new List<PostItem>();
            foreach (var p in posts.Skip(skip).Take(pager.ItemsPerPage).ToList())
            {
                items.Add(PostToItem(p, true));
            }
            return await Task.FromResult(items);
        }

        public async Task<IEnumerable<PostItem>> Search(Pager pager, string term, int author = 0, string include = "", bool sanitize = false, int currentPageId =0)
        {
            var skip = pager.CurrentPage * pager.ItemsPerPage - pager.ItemsPerPage;
                       
            var results = new List<SearchResult>();
            foreach (var p in GetPosts(include, author))
            {
                var rank = 0;
                var hits = 0;
                term = term.ToLower();

                if (p.Title.ToLower().Contains(term))
                {
                    hits = Regex.Matches(p.Title.ToLower(), term).Count;
                    rank += hits * 10;
                }
                if (p.Description.ToLower().Contains(term))
                {
                    hits = Regex.Matches(p.Description.ToLower(), term).Count;
                    rank += hits * 3;
                }
                if (p.Content.ToLower().Contains(term))
                {
                    rank += Regex.Matches(p.Content.ToLower(), term).Count;
                }

                if (rank > 0)
                {
                    results.Add(new SearchResult { Rank = rank, Item = PostToItem(p, sanitize) });
                }
            }

            results = results.OrderByDescending(r => r.Rank).ToList();

            var posts = new List<PostItem>();
            for (int i = 0; i < results.Count; i++)
            {
                if (currentPageId != 0)
                {
                    //check if the post does belong to current posts tree
                    var item = results[i].Item;
                    if (item.Id == currentPageId)
                        posts.Add(item);
                    else
                    {
                        string idPath = string.Empty;
                        if (item.ParentId != null && item.ParentId > 0 && !string.IsNullOrEmpty(item.IdPath) && !item.IdPath.Equals("/"))
                            idPath = item.IdPath;
                        else
                            idPath = GetIdPath(item);   //id path is not generated yet

                        var pages = idPath.Split('/').ToList();
                        var fItem = pages.Where(p => p.Equals(currentPageId.ToString())).FirstOrDefault();
                        if (!string.IsNullOrEmpty(fItem))
                            posts.Add(item);
                    }
                }
                else
                    posts.Add(results[i].Item);
            }
            pager.Configure(posts.Count);
            return await Task.Run(() => posts.Skip(skip).Take(pager.ItemsPerPage).ToList());
        }

        public async Task<PostItem> GetItem(Expression<Func<BlogPost, bool>> predicate, bool sanitize = false)
        {
            var post = _db.BlogPosts.Single(predicate);
            var item = PostToItem(post);

            item.Author.Avatar = string.IsNullOrEmpty(item.Author.Avatar) ? Constants.DefaultAvatar : item.Author.Avatar;
            item.Author.Email = sanitize ? Constants.DummyEmail : item.Author.Email;

            post.PostViews++;
            await _db.SaveChangesAsync();

            return await Task.FromResult(item);
        }

        public async Task<PostModel> GetModel(string slug)
        {
            var model = new PostModel();

            var all = _db.BlogPosts
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Published).ToList();

            if(all != null && all.Count > 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if(all[i].Slug == slug)
                    {
                        model.Post = PostToItem(all[i]);

                        if(i > 0 && all[i - 1].Published > DateTime.MinValue)
                        {
                            model.Newer = PostToItem(all[i - 1]);
                        }

                        if (i + 1 < all.Count && all[i + 1].Published > DateTime.MinValue)
                        {
                            model.Older = PostToItem(all[i + 1]);
                        }

                        break;
                    }
                }
            }

            var post = _db.BlogPosts.Where(p => p.Slug == slug).FirstOrDefault();
            //post.PostViews++;
            //await _db.SaveChangesAsync();

            return await Task.FromResult(model);
        }

        public async Task<PostModel> GetModelByPostId(int postId)
        {
            var model = new PostModel();

            var all = _db.BlogPosts
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Published).ToList();

            if (all != null && all.Count > 0)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Id == postId)
                    {
                        model.Post = PostToItem(all[i]);

                        if (i > 0 && all[i - 1].Published > DateTime.MinValue)
                        {
                            model.Newer = PostToItem(all[i - 1]);
                        }

                        if (i + 1 < all.Count && all[i + 1].Published > DateTime.MinValue)
                        {
                            model.Older = PostToItem(all[i + 1]);
                        }

                        break;
                    }
                }
            }

            var post = _db.BlogPosts.Single(p => p.Id == postId);
            post.PostViews++;
            await _db.SaveChangesAsync();

            return await Task.FromResult(model);
        }

        public async Task<PostItem> SaveItem(PostItem item)
        {
            BlogPost post;
            var field = _db.CustomFields.Where(f => f.AuthorId == 0 && f.Name == Constants.BlogCover).FirstOrDefault();
            var cover = field == null ? "" : field.Content;
            string idPath = GetIdPath(item);
            if (item.Id == 0)
            {
                post = new BlogPost
                {
                    Title = item.Title,
                    Slug = item.Slug,
                    Content = item.Content,
                    Description = item.Description ?? item.Title,
                    Categories = item.Categories,
                    Cover = item.Cover ?? cover,
                    AuthorId = item.Author.Id,
                    IsFeatured = item.Featured,
                    Published = item.Published,
                    ParentId = item.ParentId,
                    DisplayOrder = item.DisplayOrder==null ? 0 : (int)item.DisplayOrder,
                    IdPath = idPath,
                    NavHeader = item.NavHeader
                };
                _db.BlogPosts.Add(post);
                await _db.SaveChangesAsync();

                post = _db.BlogPosts.Single(p => p.Slug == post.Slug);
                item = PostToItem(post);
            }
            else
            {
                post = _db.BlogPosts.Single(p => p.Id == item.Id);

                post.Slug = item.Slug;
                post.Title = item.Title;
                post.Content = item.Content;
                post.Description = item.Description ?? item.Title;
                post.Categories = item.Categories;
                post.AuthorId = item.Author.Id;
                post.Published = item.Published;
                post.IsFeatured = item.Featured;
                post.ParentId = item.ParentId;
                post.DisplayOrder = item.DisplayOrder == null ? 0 : (int)item.DisplayOrder;
                post.IdPath = idPath;
                post.NavHeader = item.NavHeader;
                await _db.SaveChangesAsync();
            }
            return await Task.FromResult(item);
        }

        public async Task SaveCover(int postId, string asset)
        {
            var item = _db.BlogPosts.Single(p => p.Id == postId);
            item.Cover = asset;

            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<CategoryItem>> Categories()
        {
            var cats = new List<CategoryItem>();

            if (_db.BlogPosts.Any())
            {
                foreach (var p in _db.BlogPosts.Where(p => p.Categories != null && p.Published > DateTime.MinValue))
                {
                    var postcats = p.Categories.Split(',');
                    if (postcats.Any())
                    {
                        foreach (var pc in postcats)
                        {
                            if (!cats.Exists(c => c.Category == pc))
                            {
                                cats.Add(new CategoryItem { Category = pc, PostCount = 1 });
                            }
                            else
                            {
                                // update post count
                                var tmp = cats.Where(c => c.Category == pc).FirstOrDefault();
                                tmp.PostCount++;
                            }
                        }
                    }
                }
            }
            return await Task.FromResult(cats.OrderBy(c => c));
        }

        PostItem PostToItem(BlogPost p, bool sanitize = false)
        {
            var post = new PostItem
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                Description = p.Description,
                Content = p.Content,
                Categories = p.Categories,
                Cover = p.Cover,
                PostViews = p.PostViews,
                Rating = p.Rating,
                Published = p.Published,
                Featured = p.IsFeatured,
                Author = _db.Authors.Single(a => a.Id == p.AuthorId),
                ParentId = p.ParentId,
                DisplayOrder = p.DisplayOrder,
                IdPath = p.IdPath,
                NavHeader = p.NavHeader
            };
            if(post.Author != null)
            {
                post.Author.Avatar = string.IsNullOrEmpty(post.Author.Avatar) ?
                    AppSettings.Avatar : post.Author.Avatar;
                post.Author.Email = sanitize ? Constants.DummyEmail : post.Author.Email;
            }
            return post;
        }

        public List<PostItem> PostListToItems(List<BlogPost> posts)
        {
            return posts.Select(p => new PostItem
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                Description = p.Description,
                Content = p.Content,
                Categories = p.Categories,
                Cover = p.Cover,
                PostViews = p.PostViews,
                Rating = p.Rating,
                Published = p.Published,
                Featured = p.IsFeatured,
                Author = _db.Authors.Single(a => a.Id == p.AuthorId),
                ParentId = p.ParentId,
                DisplayOrder = p.DisplayOrder,
                IdPath = p.IdPath
            }).Distinct().ToList();
        }

        List<BlogPost> GetPosts(string include, int author)
        {
            var items = new List<BlogPost>();

            if (include.ToUpper().Contains("D") || string.IsNullOrEmpty(include))
            {
                var drafts = author > 0 ?
                    _db.BlogPosts.Where(p => p.Published == DateTime.MinValue && p.AuthorId == author).ToList() :
                    _db.BlogPosts.Where(p => p.Published == DateTime.MinValue).ToList();
                items = items.Concat(drafts).OrderBy(p => p.Slug).ToList();
            }

            if (include.ToUpper().Contains("F") || string.IsNullOrEmpty(include))
            {
                var featured = author > 0 ?
                    _db.BlogPosts.Where(p => p.Published > DateTime.MinValue && p.IsFeatured && p.AuthorId == author).ToList() :
                    _db.BlogPosts.Where(p => p.Published > DateTime.MinValue && p.IsFeatured).ToList();
                items = items.Concat(featured).OrderBy(p => p.Slug).ToList();
            }

            if (include.ToUpper().Contains("P") || string.IsNullOrEmpty(include))
            {
                var published = author > 0 ?
                    _db.BlogPosts.Where(p => p.Published > DateTime.MinValue && !p.IsFeatured && p.AuthorId == author).ToList() :
                    _db.BlogPosts.Where(p => p.Published > DateTime.MinValue && !p.IsFeatured).ToList();
                items = items.Concat(published).OrderBy(p => p.Slug).ToList();
            }

            return items;
        }

        string GetIdPath(PostItem item)
        {
            string idPath = "";
            List<int> idList = new List<int>();

            if (item.ParentId != null && item.ParentId > 0)
            {
                idList.Add((int)item.ParentId);
                var parentPost = _db.BlogPosts.Single(p => p.Id == item.ParentId);
                while (parentPost.ParentId != null && parentPost.ParentId > 0)
                {
                    idList.Add((int)parentPost.ParentId);
                    parentPost = _db.BlogPosts.Single(p => p.Id == parentPost.ParentId);
                }
                
                for(int i= idList.Count-1; i>=0; i--)
                {
                    idPath += string.Format("/{0}", idList[i].ToString());
                }
            }
            else
            {
                idPath = "/";
            }
            return idPath;
        }
        public PageCatalog GetPageCatalog()
        {
            var posts = _db.BlogPosts.Where(p => p.Published > DateTime.MinValue);
            PageCatalog pageCatalog = new PageCatalog(null, posts);
            return pageCatalog;
        }
    }

    internal class SearchResult
    {
        public int Rank { get; set; }
        public PostItem Item { get; set; }
    }
}