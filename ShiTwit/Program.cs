using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;
using RedditSharp;
using LinqToTwitter;

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
            var webAgent = new BotWebAgent(/* Nothing... */);
            var reddit = new Reddit(webAgent, false);
            //Declare shitpost subs
            List<RedditSharp.Things.Subreddit> subs = new List<RedditSharp.Things.Subreddit>();
            var me_irl = reddit.GetSubreddit("/r/me_irl");
            subs.Add(me_irl);
            var dankMemes = reddit.GetSubreddit("/r/DankMemes");
            subs.Add(dankMemes);
            while(true)
            {
                //Go through each sub in `subs`
                foreach (var sub in subs)
                {
                    //Find an image post we haven't already tweeted
                    foreach (var post in sub.Hot.Take(10))
                    {
                        //Get link to the image
                        var link = post.Url.ToString();
                        //A post is an image link if it: ends in .png, .jpg, or /maybe/ .gif
                        if (link.Contains(".jpg") || link.Contains(".png"))
                        {
                            //Get image 'name' from post link
                            int position = link.LastIndexOf("/") + 1;
                            var fileName = link.Substring(position, link.Length - position);
                            //Read list of tweets posted to check for duplicate tweet
                            bool repost = false;
                            foreach(string line in File.ReadLines("tweeted.txt"))
                            {
                                if(line.Contains(fileName))
                                {
                                    repost = true;
                                    break;
                                }
                            }
                            //If this image doesn't match any of the lines in the file, repost will be false
                            if (!repost)
                            {
                                //Write fileName to file
                                StreamWriter writer = new StreamWriter("tweeted.txt");
                                writer.WriteLine(fileName);
                                //Download image
                                WebClient wc = new WebClient();
                                wc.DownloadFile(link, "img/" + fileName);
                                Console.WriteLine(sub.Name);
                                Console.WriteLine("Downloaded image " + fileName);

                                //Tweet the image
                                Task tweetTask = TweetWithImage(twitterCtx, "img/" + fileName, post.Title, sub.Name);
                                tweetTask.Wait();
                                break;
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(3600000/2);
                }
            }           
        }    
            
        static async Task TweetWithImage(TwitterContext ctx, string file, string title, string sub)
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
            string status = sub + " - " + title;
            Status tweet = await ctx.TweetAsync(status, mediaIds);

            if (tweet != null)
                Console.WriteLine("Tweet posted: " + tweet.Text);
        }
    }
}
