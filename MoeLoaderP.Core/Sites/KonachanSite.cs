﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MoeLoaderP.Core.Sites
{
    public class KonachanSite : MoeSite
    {
        public override string HomeUrl => "https://konachan.com";
        public string SafeHomeUrl => "https://konachan.net";
        public override string DisplayName => "Konachan";
        public override string ShortName => "konachan";
        
        public KonachanSite()
        {
            DownloadTypes.Add("原图", DownloadTypeEnum.Origin);
            DownloadTypes.Add("预览图", DownloadTypeEnum.Medium); 
            Config = new MoeSiteConfig
            {
                IsSupportKeyword = true,
                IsSupportRating = true,
                IsSupportResolution = true,
                IsSupportScore = true
            };
        }

        public override async Task<MoeItems> GetRealPageImagesAsync(SearchPara para, CancellationToken token)
        {
            var homeUrl = para.IsShowExplicit ? HomeUrl : SafeHomeUrl;
            var pairs = new Pairs
                {
                    {"page",$"{para.StartPageIndex}" },
                    {"limit",$"{para.Count}" },
                    {"tags",para.Keyword.ToEncodedUrl() }
                };

            var query = $"{homeUrl}/post.json{pairs.ToPairsString()}";
            var net = new NetOperator(Settings);
            var json = await net.GetJsonAsync(query, token);
            var imageItems = new MoeItems();
            foreach (var item in Ex.GetList(json))
            {

                var img = new MoeItem(this, para);
                img.Width = $"{item.width}".ToInt();
                img.Height = $"{item.height}".ToInt();
                img.Id = $"{item.id}".ToInt();
                img.Score = $"{item.score}".ToInt();
                img.Uploader = $"{item.author}";
                img.UploaderId = $"{item.creator_id}";
                foreach (var tag in $"{item.tags}".Split(' ').SkipWhile(string.IsNullOrWhiteSpace))
                {
                    img.Tags.Add(tag.Trim());
                }

                img.IsExplicit = $"{item.rating}" == "e";
                img.DetailUrl = $"{homeUrl}/post/show/{img.Id}";
                img.Date = $"{item.created_at}".ToDateTime();
                if (img.Date == null) img.DateString = $"{item.created_at}";
                img.Urls.Add(DownloadTypeEnum.Thumbnail, $"{item.preview_url}");
                img.Urls.Add(DownloadTypeEnum.Medium, $"{item.sample_url}");
                img.Urls.Add(DownloadTypeEnum.Origin, $"{item.file_url}", img.DetailUrl, filesize: $"{item.file_size}".ToUlong());
                img.Source = $"{item.source}";
                img.OriginString = $"{item}";
                imageItems.Add(img);
            }

            return imageItems;
        }

        public override async Task<AutoHintItems> GetAutoHintItemsAsync(SearchPara para, CancellationToken token)
        {
            var net = new NetOperator(para.Site.Settings);
            var pairs = new Pairs
                {
                    {"limit", "15"},
                    {"order", "count"},
                    {"name", para.Keyword}
                };
            var query =  $"{HomeUrl}/tag.json{pairs.ToPairsString()}";
            var json =await net.GetJsonAsync(query, token);

            var items = new AutoHintItems();

            foreach (var item in Ex.GetList(json))
            {
                var hintItem = new AutoHintItem
                {
                    Count = $"{item.count}",
                    Word = $"{item.name}"
                };
                items.Add(hintItem);
            }

            return items;
        }
    }
}
