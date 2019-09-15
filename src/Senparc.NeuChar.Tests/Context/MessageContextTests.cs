﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Senparc.CO2NET.Cache;
using Senparc.CO2NET.Cache.Redis;
using Senparc.CO2NET.Extensions;
using Senparc.NeuChar.Context;
using Senparc.NeuChar.Entities;
using Senparc.Weixin.MP.Entities;
using Senparc.Weixin.MP.Entities.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Senparc.NeuChar.Tests.Context
{
    /// <summary>
    /// 消息上下文单元测试
    /// </summary>
    [TestClass]
    public class MessageContextTests : BaseTest
    {

        string textRequestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xml>
    <ToUserName><![CDATA[gh_a96a4a619366]]></ToUserName>
    <FromUserName><![CDATA[olPjZjsXuQPJoV0HlruZkNzKc91E]]></FromUserName>
    <CreateTime>{1}</CreateTime>
    <MsgType><![CDATA[text]]></MsgType>
    <Content><![CDATA[{0}]]></Content>
    <MsgId>{2}</MsgId>
</xml>
";

        public void DistributedCacheTest(Func<IBaseObjectCacheStrategy> cacheStrategy)
        {
            //强制使用本地缓存
            CacheStrategyFactory.RegisterObjectCacheStrategy(cacheStrategy);

            Console.WriteLine($"当前使用缓存：{CacheStrategyFactory.GetObjectCacheStrategyInstance().GetType().FullName}");

            //清空缓存
            var globalMessageContext = new GlobalMessageContext<CustomMessageContext, RequestMessageBase, ResponseMessageBase>();
            globalMessageContext.Restore();


            var postModel = new PostModel()
            {
                Token = "Token"
            };

            //第一次请求
            var dt1 = SystemTime.Now;
            var doc = XDocument.Parse(textRequestXml.FormatWith("TNT2", CO2NET.Helpers.DateTimeHelper.GetUnixDateTime(SystemTime.Now.UtcDateTime), SystemTime.Now.Ticks));
            var messageHandler = new CustomMessageHandler(doc, postModel);

            Assert.AreEqual(1, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(0, messageHandler.CurrentMessageContext.ResponseMessages.Count);

            messageHandler.Execute();
            Console.WriteLine($"第 1 次请求耗时：{SystemTime.NowDiff(dt1).TotalMilliseconds} ms");

            Assert.AreEqual(1, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(1, messageHandler.CurrentMessageContext.ResponseMessages.Count);
            Console.WriteLine(messageHandler.CurrentMessageContext.ResponseMessages.Last().GetType());
            Console.WriteLine(messageHandler.CurrentMessageContext.ResponseMessages.Last().ToJson());
            
            var lastResponseMessage = messageHandler.CurrentMessageContext.ResponseMessages.Last() as ResponseMessageText;
            Assert.IsNotNull(lastResponseMessage);
            Assert.AreEqual("来自单元测试:TNT2", lastResponseMessage.Content);


            //第二次请求
            var dt2 = SystemTime.Now;
            doc = XDocument.Parse(textRequestXml.FormatWith("TNT3", CO2NET.Helpers.DateTimeHelper.GetUnixDateTime(SystemTime.Now.UtcDateTime), SystemTime.Now.Ticks));
            messageHandler = new CustomMessageHandler(doc, postModel);
            Assert.AreEqual(2, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(1, messageHandler.CurrentMessageContext.ResponseMessages.Count);

            messageHandler.Execute();
            Console.WriteLine($"第 2 次请求耗时：{SystemTime.NowDiff(dt2).TotalMilliseconds} ms");

            Assert.AreEqual(2, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(2, messageHandler.CurrentMessageContext.ResponseMessages.Count);

            lastResponseMessage = messageHandler.CurrentMessageContext.ResponseMessages.Last() as ResponseMessageText;
            Assert.IsNotNull(lastResponseMessage);
            Assert.AreEqual("来自单元测试:TNT3", lastResponseMessage.Content);


            //测试去重
            var dt3 = SystemTime.Now;
            messageHandler = new CustomMessageHandler(doc, postModel);//使用和上次同样的请求
            Assert.AreEqual(2, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(2, messageHandler.CurrentMessageContext.ResponseMessages.Count);

            messageHandler.Execute();
            Console.WriteLine($"第 3 次请求耗时：{SystemTime.NowDiff(dt3).TotalMilliseconds} ms");
            //没有变化
            Assert.AreEqual(2, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(2, messageHandler.CurrentMessageContext.ResponseMessages.Count);
            lastResponseMessage = messageHandler.CurrentMessageContext.ResponseMessages.Last() as ResponseMessageText;
            Assert.IsNotNull(lastResponseMessage);
            Assert.AreEqual("来自单元测试:TNT3", lastResponseMessage.Content);


            //测试最大纪录储存



            //清空
            messageHandler.GlobalMessageContext.Restore();
            Assert.AreEqual(0, messageHandler.CurrentMessageContext.RequestMessages.Count);
            Assert.AreEqual(0, messageHandler.CurrentMessageContext.ResponseMessages.Count);

            Console.WriteLine();
        }

        [TestMethod]
        public void LocalCacheTest()
        {
            DistributedCacheTest(() => CO2NET.Cache.LocalObjectCacheStrategy.Instance);
        }

        [TestMethod]
        public void RedisCacheTest()
        {
            //注册
            var redisConfigurationStr = "localhost:6379,defaultDatabase=3";
            Senparc.CO2NET.Cache.Redis.Register.SetConfigurationOption(redisConfigurationStr);
            Senparc.CO2NET.Cache.Redis.Register.UseKeyValueRedisNow();//键值对缓存策略（推荐）

            DistributedCacheTest(() => CO2NET.Cache.Redis.RedisObjectCacheStrategy.Instance);
        }
    }
}
