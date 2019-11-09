using System;
using System.Collections.Generic;
using System.IO;
using Markdig;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable 649
#pragma warning disable 414

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class DocsController : Controller {
        private readonly Doc[] toc = {new Doc {Name = "Getting started"}, new Doc {Name = "Operations", Children = new[] {new Doc {Name = "How they work"}}}};

        [HttpGet]
        public IActionResult Get() {
            List<Doc> docsList = new List<Doc>();
            foreach (Doc doc in toc) {
                if (!string.IsNullOrEmpty(doc.Minrank)) continue;
                doc.Children = GetAll(doc);
                docsList.Add(doc);
            }

            return Ok(docsList);
        }

        private static Doc[] GetAll(Doc input) {
            List<Doc> docsList = new List<Doc>();
            foreach (Doc doc in input.Children) {
                if (!string.IsNullOrEmpty(doc.Minrank)) continue;
                doc.Children = GetAll(doc);
                docsList.Add(doc);
            }

            return docsList.ToArray();
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id) {
            string filePath = $"Docs/{id}.md";
            if (!System.IO.File.Exists(filePath)) return Ok(new {doc = $"'{filePath}' does not exist"});
            try {
                using StreamReader streamReader = new StreamReader(System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                return Ok(new {doc = Markdown.ToHtml(streamReader.ReadToEnd())});
            } catch (Exception) {
                return Ok(new {doc = $"Could not read file '{filePath}'"});
            }
        }

        private class Doc {
            public Doc[] Children = new Doc[0];
            public string Minrank;
            public string Name;
        }
    }
}
