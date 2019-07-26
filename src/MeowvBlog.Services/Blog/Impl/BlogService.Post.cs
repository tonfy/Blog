﻿using MeowvBlog.Core.Domain.Blog;
using MeowvBlog.Core.Domain.Blog.Repositories;
using MeowvBlog.Services.Dto;
using MeowvBlog.Services.Dto.Blog;
using Plus;
using Plus.AutoMapper;
using Plus.Services.Dto;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MeowvBlog.Services.Blog.Impl
{
    public partial class BlogService : ServiceBase, IBlogService
    {
        private readonly IPostRepository _postRepository;
        private readonly ITagRepository _tagRepository;
        private readonly IPostTagRepository _postTagRepository;
        private readonly ICategoryRepository _categoryRepository;

        public BlogService(
            IPostRepository postRepository,
            ITagRepository tagRepository,
            IPostTagRepository postTagRepository,
            ICategoryRepository categoryRepository)
        {
            _postRepository = postRepository;
            _tagRepository = tagRepository;
            _postTagRepository = postTagRepository;
            _categoryRepository = categoryRepository;
        }

        /// <summary>
        /// 新增文章
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<ActionOutput<string>> InsertPost(PostDto dto)
        {
            using (var uow = UnitOfWorkManager.Begin())
            {
                var output = new ActionOutput<string>();
                var post = new Post
                {
                    Title = dto.Title,
                    Author = dto.Author,
                    Url = dto.Url,
                    Content = dto.Content,
                    CreationTime = dto.CreationTime
                };

                var result = await _postRepository.InsertAsync(post);
                await uow.CompleteAsync();

                if (result.IsNull())
                    output.AddError("新增文章出错了~~~");
                else
                    output.Result = "success";

                return output;
            }
        }

        /// <summary>
        /// 删除文章
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ActionOutput<string>> DeletePost(int id)
        {
            using (var uow = UnitOfWorkManager.Begin())
            {
                var output = new ActionOutput<string>();

                await _postRepository.DeleteAsync(id);
                await uow.CompleteAsync();

                output.Result = "success";

                return output;
            }
        }

        /// <summary>
        /// 更新文章
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<ActionOutput<string>> UpdatePost(int id, PostDto dto)
        {
            using (var uow = UnitOfWorkManager.Begin())
            {
                var output = new ActionOutput<string>();

                var post = new Post
                {
                    Id = id,
                    Title = dto.Title,
                    Author = dto.Author,
                    Url = dto.Url,
                    Content = dto.Content,
                    CreationTime = dto.CreationTime
                };

                var result = await _postRepository.UpdateAsync(post);
                await uow.CompleteAsync();

                if (result.IsNull())
                    output.AddError("更新文章出错了~~~");
                else
                    output.Result = "success";

                return output;
            }
        }

        /// <summary>
        /// 获取文章
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<ActionOutput<GetPostDto>> GetPost(string url)
        {
            var output = new ActionOutput<GetPostDto>();

            using (var uow = UnitOfWorkManager.Begin())
            {
                var post = await _postRepository.FirstOrDefaultAsync(x => x.Url == url);
                if (post.IsNull())
                {
                    output.AddError("找了找不到了~~~");
                    return output;
                }

                var category = await _categoryRepository.FirstOrDefaultAsync(x => x.Id == post.CategoryId);

                var tags = (from post_tags in await _postTagRepository.GetAllListAsync()
                            join tag in await _tagRepository.GetAllListAsync()
                            on post_tags.TagId equals tag.Id
                            where post_tags.PostId == post.Id
                            select new TagDto
                            {
                                TagName = tag.TagName,
                                DisplayName = tag.DisplayName
                            }).ToList();

                var previous = _postRepository.GetAll()
                                              .Where(x => x.CreationTime > post.CreationTime)
                                              .Take(1)
                                              .FirstOrDefault();

                var next = _postRepository.GetAll()
                                          .Where(x => x.CreationTime < post.CreationTime)
                                          .OrderByDescending(x => x.CreationTime)
                                          .Take(1)
                                          .FirstOrDefault();

                await uow.CompleteAsync();

                var result = post.MapTo<GetPostDto>();
                result.CreationTime = Convert.ToDateTime(result.CreationTime).ToString("MMMM dd, yyyy HH:mm:ss", new CultureInfo("en-us"));
                result.Category = category.MapTo<CategoryDto>();
                result.Tags = tags;
                result.Previous = previous.MapTo<PostForPagedDto>();
                result.Next = next.MapTo<PostForPagedDto>();

                output.Result = result;

                return output;
            }
        }

        /// <summary>
        /// 分页查询文章列表
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<PagedResultDto<PostBriefDto>> QueryPosts(PagingInput input)
        {
            var posts = await _postRepository.GetAllListAsync();

            var count = posts.Count;

            var list = posts.OrderByDescending(x => x.CreationTime).AsQueryable().PageByIndex(input.Page, input.Limit).ToList();

            var result = list.MapTo<IList<PostBriefDto>>().ToList();
            result.ForEach(x =>
            {
                x.CreationTime = Convert.ToDateTime(x.CreationTime).ToString("MMMM dd, yyyy HH:mm:ss", new CultureInfo("en-us"));
                x.Year = Convert.ToDateTime(x.CreationTime).Year;
            });

            return new PagedResultDto<PostBriefDto>(count, result);
        }

        /// <summary>
        /// 通过标签查询文章列表
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<IList<QueryPostDto>> QueryPostsByTag(string name)
        {
            var query = (from post_tags in await _postTagRepository.GetAllListAsync()
                         join tags in await _tagRepository.GetAllListAsync()
                         on post_tags.TagId equals tags.Id
                         join posts in await _postRepository.GetAllListAsync()
                         on post_tags.PostId equals posts.Id
                         where tags.DisplayName == name
                         orderby posts.CreationTime descending
                         select new PostBriefDto
                         {
                             Title = posts.Title,
                             Url = posts.Url,
                             CreationTime = posts.CreationTime?.ToString("MMMM dd, yyyy HH:mm:ss", new CultureInfo("en-us")),
                             Year = posts.CreationTime.Value.Year
                         }).GroupBy(x => x.Year).ToList();

            var result = new List<QueryPostDto>();

            query.ForEach(x =>
            {
                result.Add(new QueryPostDto
                {
                    Year = x.Key,
                    Posts = x.ToList()
                });
            });

            return result;
        }

        /// <summary>
        /// 通过分类查询文章列表
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<IList<QueryPostDto>> QueryPostsByCategory(string name)
        {
            var query = (from posts in await _postRepository.GetAllListAsync()
                         join categories in await _categoryRepository.GetAllListAsync()
                         on posts.CategoryId equals categories.Id
                         where categories.DisplayName == name
                         orderby posts.CreationTime descending
                         select new PostBriefDto
                         {
                             Title = posts.Title,
                             Url = posts.Url,
                             CreationTime = posts.CreationTime?.ToString("MMMM dd, yyyy HH:mm:ss", new CultureInfo("en-us")),
                             Year = posts.CreationTime.Value.Year
                         }).GroupBy(x => x.Year).ToList();

            var result = new List<QueryPostDto>();

            query.ForEach(x =>
            {
                result.Add(new QueryPostDto
                {
                    Year = x.Key,
                    Posts = x.ToList()
                });
            });

            return result;
        }
    }
}