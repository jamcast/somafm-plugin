/*-
 * Copyright (c) 2015 Software Development Solutions, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS ``AS IS'' AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
 * OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
 * HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
 * LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
 * OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml;

namespace Jamcast.Plugins.SomaFM
{
    internal class Soma
    {
        private const string CHANNELS_XML_URL = "http://somafm.com/channels.xml";

        private static List<Channel> _channels;

        internal static Channel GetChannel(string id)
        {
            if (_channels == null)
            {
                GetChannels();
                if (_channels == null)
                    throw new ApplicationException("Channel cache is null");
            }
            return (from Channel c in _channels where c.ID.Equals(id) select c).FirstOrDefault();
        }

        internal static Channel[] GetChannels()
        {
            if (_channels != null)
                return _channels.ToArray();
            var request = HttpWebRequest.Create(CHANNELS_XML_URL) as HttpWebRequest;
            var response = request.GetResponse();
            List<Channel> channels = new List<Channel>();
            using (var reader = XmlReader.Create(response.GetResponseStream()))
            {
                if (!reader.ReadToFollowing("channel"))
                    throw new ApplicationException("Invalid channel XML");
                do
                {
                    var channel = new Channel();
                    if (!reader.MoveToAttribute("id"))
                        throw new ApplicationException("Invalid channel XML");
                    channel.ID = reader.Value;
                    reader.MoveToElement();
                    var reader2 = reader.ReadSubtree();
                    while (reader2.Read())
                    {
                        switch (reader2.Name)
                        {
                            case "title":
                                channel.Title = reader2.ReadString();
                                break;

                            case "fastpls":
                            case "slowpls":
                            case "highestpls":
                                var streamUrl = new ChannelStreamUrl()
                                {
                                    StreamType = reader2.Name
                                };
                                if (!reader2.MoveToAttribute("format"))
                                    throw new ApplicationException("Invalid channel XML");
                                streamUrl.Format = reader2.Value;
                                //reader.MoveToElement();
                                streamUrl.Url = reader.ReadString();
                                channel.Streams.Add(streamUrl);
                                break;

                            case "image":
                            case "largeimage":
                            case "xlimage":
                                var imgUrl = new ChannelImageUrl()
                                {
                                    ImageType = reader2.Name,
                                    Url = reader2.ReadString()
                                };
                                channel.Images.Add(imgUrl);
                                break;
                        }
                    }
                    channels.Add(channel);
                }
                while (reader.ReadToFollowing("channel"));
                _channels = channels;
                return channels.ToArray();
            }
        }

        public class Channel
        {
            public string Title { get; set; }

            public List<ChannelImageUrl> Images { get; set; }

            public List<ChannelStreamUrl> Streams { get; set; }

            public string ID { get; set; }

            public Channel()
            {
                this.Images = new List<ChannelImageUrl>();
                this.Streams = new List<ChannelStreamUrl>();
            }

            public ChannelImageUrl GetBestImage()
            {
                var typePrefs = new string[] { "largeimage", "xlimage", "image" };
                foreach (string type in typePrefs)
                {
                    var url = (from image in this.Images where image.ImageType.Equals(type) select image).FirstOrDefault();
                    if (url != null)
                        return url;
                }
                return null;
            }

            public ChannelStreamUrl GetBestStream(string format)
            {
                var typePrefs = new string[] { "highestpls", "fastpls", "slowpls" };
                foreach (string type in typePrefs)
                {
                    var url = (from stream in this.Streams where stream.StreamType.Equals(type) && stream.Format.Equals(format, StringComparison.InvariantCultureIgnoreCase) select stream).FirstOrDefault();
                    if (url != null)
                        return url;
                }
                return null;
            }
        }

        public class ChannelStreamUrl
        {
            public string Url { get; set; }

            public string StreamType { get; set; }

            public string Format { get; set; }
        }

        public class ChannelImageUrl
        {
            public string Url { get; set; }

            public string ImageType { get; set; }
        }
    }
}