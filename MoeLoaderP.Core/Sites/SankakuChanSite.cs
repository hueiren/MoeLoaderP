﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MoeLoaderP.Core.Sites
{
    /// <summary>
    /// chan.sankakucomplex.com
    /// </summary>
    public class SankakuChanSite : MoeSite
    {
        public override string HomeUrl => "https://chan.sankakucomplex.com";

        public override string DisplayName => "SankakuComplex[Chan]";

        public override string ShortName => "sankakucomplex-chan";
        public override string[] GetCookieUrls()
        {
            return new[]{ "https://beta.sankakucomplex.com" };
        }

        public string Api = "https://capi-v2.sankakucomplex.com";
        public string BetaApi = "https://beta.sankakucomplex.com";

        public SankakuChanSite()
        {
            DownloadTypes.Add("原图", DownloadTypeEnum.Origin);
            LoginPageUrl = "https://beta.sankakucomplex.com/home";

            Config = new MoeSiteConfig
            {
                IsSupportKeyword = true,
                IsSupportRating = true,
                IsSupportResolution = true,
                IsSupportScore = true,
                IsSupportAccount = true,
                IsSupportStarButton = true,
                IsSupportMultiKeywords = true,
            };
            Lv2Cat = new Categories()
            {
                new("最新/搜索"),
                new Category()
                {
                    Name = "收藏",
                    OverrideConfig = new MoeSiteConfig()
                    {
                        IsSupportAccount = true,
                    }
                }
            };
            Config.ImageOrders = new ImageOrders()
            {
                new() { Name = "最新", Order = ImageOrderBy.Date },
                new() { Name = "最受欢迎", Order = ImageOrderBy.Popular }
            };
        }
        private string AccessToken => SiteSettings.GetSetting("accessToken");
        public override void Logout()
        {
            SiteSettings.Items.Remove("accessToken");
            base.Logout();
        }

        public override async Task<bool> StarAsync(MoeItem moe,CancellationToken token)
        {
            var net = CloneNet();
            net.SetReferer(BetaApi);
            var res = await net.PostAsync($"https://capi-v2.sankakucomplex.com/posts/{moe.Id}/favorite?lang=en", token);
            var job = res as JObject;
            var b = job?["success"]?.Value<bool?>();

            if (b == true)
            {
                moe.IsFav = true;
                var count = job["score"]?.Value<int>();
                if (count != null) moe.FavCount = count.Value;
                return true;
            }
            
            return false;
        }

        public void Login()
        {
            Net = new NetOperator(Settings);
            var cc = SiteSettings.GetCookieContainer();
            if (cc != null) Net.SetCookie(cc);
            IsUserLogin = AccessToken != null;
        }

        public NetOperator CloneNet()
        {
            var net = Net.CreateNewWithOldCookie();
            if (!AccessToken.IsEmpty())
            {
                net.Client.DefaultRequestHeaders.Add("authorization", $"Bearer {AccessToken}");
            }
            
            return net;
        }

        public override bool VerifyCookieAndSave(CookieCollection ccol)
        {
            
            foreach (Cookie cookie in ccol)
            {
                if (cookie.Name.Equals("accessToken", StringComparison.OrdinalIgnoreCase))
                {
                    SiteSettings.SetSetting("accessToken", cookie.Value);
                    return true;
                }
            }

            return false;
        }

        public string ConbimeMultiKeywords(params string[] kws)
        {
            string s = "";
            foreach (var kw in kws)
            {
                if (!kw.IsEmpty())
                {
                    s += kw + "+";
                }
            }

            return s[..^1];
        }


        public override async Task<MoeItems> GetRealPageImagesAsync(SearchPara para, CancellationToken token)
        {
            return await GetNewAndTagAsync(para, token);
        }

        public async Task<MoeItems> GetNewAndTagAsync(SearchPara para, CancellationToken token)
        {
            var imgs = new MoeItems();
            if (Net == null) Login();
            var net = CloneNet();
            net.SetReferer(BetaApi);

            string kw;
            if (para.IsShowExplicit == false)
            {
                kw = ConbimeMultiKeywords("rating:safe", para.Keyword.Trim().Replace(" ", "_"));
            }
            else
            {
                kw = para.Keyword.Trim().Replace(" ","_");
            }

            var pairs = new Pairs
            {
                { "lang", "en" },
                { "next", $"{para.NextPageMark}" },
                { "limit", $"{para.Count}" },
                { "hide_posts_in_books", "in-larger-tags" },
                { "default_threshold", "1" },

            };

            if (para.Lv2MenuIndex == 1)
            {
                var meapi = "https://capi-v2.sankakucomplex.com/users/me?lang=en";
                var menet = CloneNet();
                var mejson = await menet.GetJsonAsync(meapi, token);
                var username = mejson?.user?.name;
                if (username != null)
                {
                    pairs.Add("tags", $"Fav:{username}");
                }
                else
                {
                    Ex.ShowMessage("无法获取Username");
                    return null;
                }
            }
            else
            {
                string kw2;
                if (para.MultiKeywords.Count > 0)
                {
                    var list = new List<string>();
                    if (!kw.IsEmpty())
                    {
                        list.Add(kw);
                    }
                    
                    foreach (var k in para.MultiKeywords)
                    {
                        list.Add(k.Replace(" ","_"));
                    }
                    kw2 = ConbimeMultiKeywords(list.ToArray());
                }
                else
                {
                    kw2 = kw;
                }
                pairs.Add("tags", kw2);
            }

            var json = await net.GetJsonAsync($"{Api}/posts/keyset", token, pairs);
            if (json == null) return null;
            //if($"{json.suce}")
            para.NextPageMark = $"{json.meta?.next}";
            foreach (var jitem in Ex.GetList(json.data))
            {
                var img = new MoeItem(this, para)
                {
                    Net = CloneNet(),
                    Id = $"{jitem.id}".ToInt(),
                    Width = $"{jitem.width}".ToInt(),
                    Height = $"{jitem.height}".ToInt(),
                    Score = $"{jitem.total_score}".ToInt()
                };
                img.Urls.Add(1, $"{jitem.preview_url}", BetaApi);
                img.Urls.Add(2, $"{jitem.sample_url}", BetaApi);
                img.Urls.Add(4, $"{jitem.file_url}", $"{BetaApi}/post/show/{img.Id}");
                img.IsExplicit = $"{jitem.rating}" != "s";
                img.Date = $"{jitem.created_at?.s}".ToDateTime();
                img.Uploader = $"{jitem.author?.name}";
                img.DetailUrl = $"{BetaApi}/post/show/{img.Id}";
                img.IsFav = $"{jitem.is_favorited}".ToLower() == "true";
                img.FavCount = $"{jitem.fav_count}".ToInt();
                if ($"{jitem.redirect_to_signup}".ToLower() == "true")
                {
                    img.Tip = "此图片需要登录查看";
                }

                foreach (var tag in Ex.GetList(jitem.tags))
                {
                    img.Tags.Add($"{tag.name_en}");
                }

                img.OriginString = $"{jitem}";

                imgs.Add(img);
            }

            return imgs;
        }

        public override async Task<AutoHintItems> GetAutoHintItemsAsync(SearchPara para, CancellationToken token)
        {
            var ahitems = new AutoHintItems();
            const string api = "https://capi-v2.sankakucomplex.com";
            if (Net == null) Login();
            var net = CloneNet();
            var pairs = new Pairs
            {
                { "lang", "en" },
                { "tag", para.Keyword.ToEncodedUrl() },
                { "target", "post" },
                { "show_meta", "1" }
            };
            var json = await net.GetJsonAsync($"{api}/tags/autosuggestCreating", token, pairs);
            foreach (var jitem in Ex.GetList(json))
            {
                var ahitem = new AutoHintItem();
                ahitem.Word = $"{jitem.name}";
                ahitem.Count = $"{jitem.count}";
                ahitems.Add(ahitem);
            }

            return ahitems;

        }
    }

}
