﻿using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using SolidifyProject.Engine.Infrastructure.Interfaces;
using SolidifyProject.Engine.Infrastructure.Models;
using SolidifyProject.Engine.Infrastructure.Models.Base;

namespace SolidifyProject.Engine.Infrastructure.Services
{
    public sealed class OrchestrationService
    {
        public ILoggerService LoggerService { get; set; }

        public IContentReaderService<BinaryContentModel> AssetsReaderService { get; set; }
        public IContentWriterService<BinaryContentModel> AssetsWriterService { get; set; }
        
        public IContentReaderService<PageModel> PageModelReaderService { get; set; }
        public IContentWriterService<TextContentModel> PageModelWriterService { get; set; }
        
        public IContentReaderService<TemplateModel> TemplateReaderService { get; set; }
        public ITemplateService TemplateService { get; set; }

        public IDataService DataService { get; set; }

        public IMarkupService MarkupService { get; set; }

        public IHtmlMinificationService HtmlMinificationService { get; set; }
        
        public async Task RenderProjectAsync()
        {
            await CleanOutputAsync();
            
            var tasksGroupContent = Task.Run(async () =>
            {
                var dataModel = await DataService.GetDataModelAsync();

                var pages = await PageModelReaderService.LoadContentsIdsAsync();
                var pageTasks = pages.Select(pageId => ProcessPageByIdAsync(pageId, dataModel)).ToList();
            
                await Task.WhenAll(pageTasks).ConfigureAwait(false);
            });
            
            var tasksGroupAssets = Task.Run(async () =>
            {
                var assets = await AssetsReaderService.LoadContentsIdsAsync();

                var tasks = assets.Select(CopyAssetByIdAsync).ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            });

            await Task.WhenAll(tasksGroupContent, tasksGroupAssets).ConfigureAwait(false);
        }

        private async Task ProcessPageByIdAsync(string pageId, ExpandoObject dataModel)
        {
            await LoggerService.WriteLogMessage($"{DateTime.Now.ToLongTimeString()}: [Page:Started] \"{pageId}\"");
            
            var page = await PageModelReaderService.LoadContentByIdAsync(pageId).ConfigureAwait(false);
            
            page.MapDataToModel(dataModel);
            
            var html = await TemplateService.RenderTemplateAsync(page.Content, page, dataModel);
            html = await MarkupService.RenderMarkupAsync(html).ConfigureAwait(false);
            page.Content = html;
                
            var template = await TemplateReaderService.LoadContentByIdAsync(page.TemplateId).ConfigureAwait(false);
            html = await TemplateService.RenderTemplateAsync(template.Template, page, dataModel).ConfigureAwait(false);

            html = await HtmlMinificationService.CompressHtmlAsync(html).ConfigureAwait(false);

            var result = new TextContentModel();
            result.Id = page.Id;
            result.ContentRaw = html;
                
            await PageModelWriterService.SaveContentAsync(page.Url, result).ConfigureAwait(false);
            
            await LoggerService.WriteLogMessage($"{DateTime.Now.ToLongTimeString()}: [Page:Finished] \"{pageId}\"");
        }

        private async Task CopyAssetByIdAsync(string id)
        {
            await LoggerService.WriteLogMessage($"{DateTime.Now.ToLongTimeString()}: [Asset:Started] \"{id}\"");
            
            var content = await AssetsReaderService.LoadContentByIdAsync(id).ConfigureAwait(false);

            await AssetsWriterService.SaveContentAsync(id, content).ConfigureAwait(false);
            
            await LoggerService.WriteLogMessage($"{DateTime.Now.ToLongTimeString()}: [Asset:Finished] \"{id}\"");
        }

        private async Task CleanOutputAsync()
        {
            await LoggerService.WriteLogMessage($"{DateTime.Now.ToLongTimeString()}: Starting cleaning output folder");
            
            await PageModelWriterService.CleanFolderAsync(String.Empty);

            await AssetsWriterService.CleanFolderAsync(String.Empty);

            await LoggerService.WriteLogMessage($"{DateTime.Now.ToLongTimeString()}: Cleaning output folder finished");
        }
    }
}