using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;
using RedditSharp;
using LinqToTwitter;
using ShiTwit;

namespace ShiTwit
{
    class Program
    {
        static void Main(string[] args)
        {
            //Connect to twitter
            var auth = new SingleUserAuthorizer
            {
                CredentialStore = new SingleUserInMemoryCredentialStore
                {
                    ConsumerKey = ConfigurationManager.AppSettings["consumerKey"],
                    ConsumerSecret = ConfigurationManager.AppSettings["consumerSecret"],
                    AccessToken = ConfigurationManager.AppSettings["accessToken"],
                    AccessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"]

                }
            };
            var twitterCtx = new TwitterContext(auth);

            //Connect to reddit
            var webAgent = new BotWebAgent("shitwit_bot",
                "", "", 
                "", "https://www.thenightmancodeth.me/");
            var reddit = new Reddit(webAgent, false);       
            //Declare shitpost subs
            var me_irl = reddit.GetSubreddit("/r/me_irl");
            //Get top 5 posts from each sub TODO
            foreach (var post in me_irl.Hot.Take(10))
            {
                //Make sure post type is an image link
                var link = post.Url.ToString();
                //A post is an image link if it: ends in .png, .jpg, or contains 'i.reddituploads.com'
                if (link.Contains(".jpg") || link.Contains(".png"))
                {
                    //This post is an image!!!
                    try
                    {
                        //Get image 'name' from post link
                        int position = link.LastIndexOf("/") + 1;
                        var fileName = link.Substring(position, link.Length - position);
                        //Get list of images already tweeted
                        string[] alreadyPosted = Directory.GetFiles("img/");       
                        
                        bool repost = false;
                        if(alreadyPosted.Length > 1)
                        {
                            foreach (string name in alreadyPosted)
                            {
                                if(name.Contains(fileName))
                                {
                                    repost = true;
                                    break;
                                }
                            }                       
                        }

                        if (!repost)
                        {
                            //Download image
                            WebClient wc = new WebClient();
                            wc.DownloadFile(link, "img/" + fileName);
                            Console.WriteLine("Downloaded image " + fileName);

                            //Tweet the image
                            Task tweetTask = TweetWithImage(twitterCtx, "img/" + fileName, post.Title);
                            tweetTask.Wait();
                        }

                    } catch (System.ArgumentOutOfRangeException e)
                    {
                        Console.Write(e.StackTrace);
                    }
                    
                }
                System.Threading.Thread.Sleep(10800000);
            }
        }
        static async Task TweetWithImage(TwitterContext ctx, string file, string title)
        {
            string imgType;
            if (file.EndsWith(".png"))
            {
                imgType = "image/png";
            }
            else
            {
                imgType = "image/jpg";
            }
            var imgUploadTasks =
                new List<Task<Media>>
                {
                    ctx.UploadMediaAsync(File.ReadAllBytes(file), imgType)
                };
            await Task.WhenAll(imgUploadTasks);

            var mediaIds =
                (from tsk in imgUploadTasks
                 select tsk.Result.MediaID)
                 .ToList();

            Status tweet = await ctx.TweetAsync(title, mediaIds);

            if (tweet != null)
                Console.WriteLine("Tweet posted: " + tweet.Text);
        }
    }
}
