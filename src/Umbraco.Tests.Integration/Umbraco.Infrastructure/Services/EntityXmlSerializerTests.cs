// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Cms.Tests.Integration.Umbraco.Infrastructure.Services.Importing;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.PropertyEditors;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Strings;

namespace Umbraco.Cms.Tests.Integration.Umbraco.Infrastructure.Services
{
    [TestFixture]
    [UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
    public class EntityXmlSerializerTests : UmbracoIntegrationTest
    {
        private IEntityXmlSerializer Serializer => GetRequiredService<IEntityXmlSerializer>();
        private IContentService ContentService => GetRequiredService<IContentService>();
        private IMediaService MediaService => GetRequiredService<IMediaService>();
        private IUserService UserService => GetRequiredService<IUserService>();
        private IMediaTypeService MediaTypeService => GetRequiredService<IMediaTypeService>();
        private IDataValueEditorFactory DataValueEditorFactory => GetRequiredService<IDataValueEditorFactory>();
        private ILocalizedTextService TextService => GetRequiredService<ILocalizedTextService>();

        [Test]
        public void Can_Export_Macro()
        {
            // Arrange
            IMacroService macroService = GetRequiredService<IMacroService>();
            Macro macro = new MacroBuilder()
                .WithAlias("test1")
                .WithName("Test")
                .Build();
            macroService.Save(macro);

            // Act
            XElement element = Serializer.Serialize(macro);

            // Assert
            Assert.That(element, Is.Not.Null);
            Assert.That(element.Element("name").Value, Is.EqualTo("Test"));
            Assert.That(element.Element("alias").Value, Is.EqualTo("test1"));
            Debug.Print(element.ToString());
        }

        [Test]
        public void Can_Export_DictionaryItems()
        {
            // Arrange
            CreateDictionaryData();
            ILocalizationService localizationService = GetRequiredService<ILocalizationService>();
            IDictionaryItem dictionaryItem = localizationService.GetDictionaryItemByKey("Parent");

            var newPackageXml = XElement.Parse(ImportResources.Dictionary_Package);
            XElement dictionaryItemsElement = newPackageXml.Elements("DictionaryItems").First();

            // Act
            XElement xml = Serializer.Serialize(new[] { dictionaryItem });

            // Assert
            Assert.That(xml.ToString(), Is.EqualTo(dictionaryItemsElement.ToString()));
        }

        [Test]
        public void Can_Export_Languages()
        {
            // Arrange
            ILocalizationService localizationService = GetRequiredService<ILocalizationService>();

            ILanguage languageNbNo = new LanguageBuilder()
                .WithCultureInfo("nb-NO")
                .WithCultureName("Norwegian")
                .Build();
            localizationService.Save(languageNbNo);

            ILanguage languageEnGb = new LanguageBuilder()
                .WithCultureInfo("en-GB")
                .Build();
            localizationService.Save(languageEnGb);

            var newPackageXml = XElement.Parse(ImportResources.Dictionary_Package);
            XElement languageItemsElement = newPackageXml.Elements("Languages").First();

            // Act
            XElement xml = Serializer.Serialize(new[] { languageNbNo, languageEnGb });

            // Assert
            Assert.That(xml.ToString(), Is.EqualTo(languageItemsElement.ToString()));
        }

        [Test]
        public void Can_Generate_Xml_Representation_Of_Media()
        {
            // Arrange
            var mediaType = MediaTypeBuilder.CreateImageMediaType("image2");

            MediaTypeService.Save(mediaType);

            // reference, so static ctor runs, so event handlers register
            // and then, this will reset the width, height... because the file does not exist, of course ;-(
            var loggerFactory = NullLoggerFactory.Instance;
            var scheme = Mock.Of<IMediaPathScheme>();
            var contentSettings = new ContentSettings();

            var mediaFileManager = new MediaFileManager(
                Mock.Of<IFileSystem>(),
                scheme,
                loggerFactory.CreateLogger<MediaFileManager>(),
                ShortStringHelper,
                Services,
                Options.Create(new ContentSettings()));

            var ignored = new FileUploadPropertyEditor(
                DataValueEditorFactory,
                mediaFileManager,
                Options.Create(contentSettings),
                TextService,
                Services.GetRequiredService<UploadAutoFillProperties>(),                
                ContentService,
                IOHelper);

            var media = MediaBuilder.CreateMediaImage(mediaType, -1);
            media.WriterId = -1; // else it's zero and that's not a user and it breaks the tests
            MediaService.Save(media, Constants.Security.SuperUserId);

            // so we have to force-reset these values because the property editor has cleared them
            media.SetValue(Constants.Conventions.Media.Width, "200");
            media.SetValue(Constants.Conventions.Media.Height, "200");
            media.SetValue(Constants.Conventions.Media.Bytes, "100");
            media.SetValue(Constants.Conventions.Media.Extension, "png");

            var nodeName = media.ContentType.Alias.ToSafeAlias(ShortStringHelper);
            var urlName = media.GetUrlSegment(ShortStringHelper, new[] { new DefaultUrlSegmentProvider(ShortStringHelper) });

            // Act
            XElement element = media.ToXml(Serializer);

            // Assert
            Assert.That(element, Is.Not.Null);
            Assert.That(element.Name.LocalName, Is.EqualTo(nodeName));
            Assert.AreEqual(media.Id.ToString(), (string)element.Attribute("id"));
            Assert.AreEqual(media.ParentId.ToString(), (string)element.Attribute("parentID"));
            Assert.AreEqual(media.Level.ToString(), (string)element.Attribute("level"));
            Assert.AreEqual(media.SortOrder.ToString(), (string)element.Attribute("sortOrder"));
            Assert.AreEqual(media.CreateDate.ToString("s"), (string)element.Attribute("createDate"));
            Assert.AreEqual(media.UpdateDate.ToString("s"), (string)element.Attribute("updateDate"));
            Assert.AreEqual(media.Name, (string)element.Attribute("nodeName"));
            Assert.AreEqual(urlName, (string)element.Attribute("urlName"));
            Assert.AreEqual(media.Path, (string)element.Attribute("path"));
            Assert.AreEqual("", (string)element.Attribute("isDoc"));
            Assert.AreEqual(media.ContentType.Id.ToString(), (string)element.Attribute("nodeType"));
            Assert.AreEqual(media.GetCreatorProfile(UserService).Name, (string)element.Attribute("writerName"));
            Assert.AreEqual(media.CreatorId.ToString(), (string)element.Attribute("writerID"));
            Assert.IsNull(element.Attribute("template"));

            Assert.AreEqual(media.Properties[Constants.Conventions.Media.File].GetValue().ToString(), element.Elements(Constants.Conventions.Media.File).Single().Value);
            Assert.AreEqual(media.Properties[Constants.Conventions.Media.Width].GetValue().ToString(), element.Elements(Constants.Conventions.Media.Width).Single().Value);
            Assert.AreEqual(media.Properties[Constants.Conventions.Media.Height].GetValue().ToString(), element.Elements(Constants.Conventions.Media.Height).Single().Value);
            Assert.AreEqual(media.Properties[Constants.Conventions.Media.Bytes].GetValue().ToString(), element.Elements(Constants.Conventions.Media.Bytes).Single().Value);
            Assert.AreEqual(media.Properties[Constants.Conventions.Media.Extension].GetValue().ToString(), element.Elements(Constants.Conventions.Media.Extension).Single().Value);
        }

        private void CreateDictionaryData()
        {
            ILocalizationService localizationService = GetRequiredService<ILocalizationService>();

            ILanguage languageNbNo = new LanguageBuilder()
                .WithCultureInfo("nb-NO")
                .WithCultureName("Norwegian")
                .Build();
            localizationService.Save(languageNbNo);

            ILanguage languageEnGb = new LanguageBuilder()
                .WithCultureInfo("en-GB")
                .Build();
            localizationService.Save(languageEnGb);

            var parentItem = new DictionaryItem("Parent") { Key = Guid.Parse("28f2e02a-8c66-4fcd-85e3-8524d551c0d3") };
            var parentTranslations = new List<IDictionaryTranslation>
            {
                new DictionaryTranslation(languageNbNo, "ForelderVerdi"),
                new DictionaryTranslation(languageEnGb, "ParentValue")
            };
            parentItem.Translations = parentTranslations;
            localizationService.Save(parentItem);

            var childItem = new DictionaryItem(parentItem.Key, "Child") { Key = Guid.Parse("e7dba0a9-d517-4ba4-8e18-2764d392c611") };
            var childTranslations = new List<IDictionaryTranslation>
            {
                new DictionaryTranslation(languageNbNo, "BarnVerdi"),
                new DictionaryTranslation(languageEnGb, "ChildValue")
            };
            childItem.Translations = childTranslations;
            localizationService.Save(childItem);
        }
    }
}
