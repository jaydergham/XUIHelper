﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace XUIHelper.Core
{
    [Serializable, XmlRoot("XUIHelperExtensions")]
    public class XUIHelperExtensions
    {
        [XmlElement("XUIClassExtension")]
        public XUClassExtension Extensions { get; set; }

        [XmlElement("XUIClass")]
        public XUIHelperIgnoreProperties IgnoreProperties { get; set; }

        public XUIHelperExtensions()
        {

        }
    }
}
