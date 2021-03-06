using System;
using System.Collections.Generic;
using Yngdieng.Protos;
using Yngdieng.Common;
using Google.Protobuf;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;

namespace Yngdieng.Indexer
{
  public class DocumentAggregator
  {
    public DocumentAggregator()
    {
    }

    private readonly IDictionary<string, AggregatedDocument> documents = new Dictionary<string, AggregatedDocument>();

    public void Add(Document d)
    {
      var key =
      Base64UrlTextEncoder.Encode(new DocumentAggregateKey
      {
        HanziCanonical = d.HanziCanonical,
        Initial = d.Initial,
        Final = d.Final,
        Tone = d.Tone
      }.ToByteArray());

      if (!documents.ContainsKey(key))
      {
        documents[key] = new AggregatedDocument()
        {
          Id = key
        };
      }
      documents[key].Initial = d.Initial;
      documents[key].Final = d.Final;
      documents[key].Tone = d.Tone;
      documents[key].HanziCanonical = d.HanziCanonical;
      documents[key].Buc = d.Buc;
      documents[key].Yngping = YngpingBekinUtil.FanqieToYngping(d.Initial, d.Final, d.Tone);

      var altA = documents[key].HanziAlternatives;
      var altB = d.HanziAlternatives;
      var altMerged = altA.Union(altB).Distinct().ToList();
      documents[key].HanziAlternatives.Clear();
      documents[key].HanziAlternatives.AddRange(altMerged);

      var mA = documents[key].HanziMatchable;
      var mB = d.HanziMatchable;
      var mMerged = mA.Union(mB).Distinct().ToList();
      documents[key].HanziMatchable.Clear();
      documents[key].HanziMatchable.AddRange(mMerged);

      if (d.SourceInfoCase == Document.SourceInfoOneofCase.Ciklin)
      {
        documents[key].CiklinSource = d.Ciklin;
      }

      if (d.SourceInfoCase == Document.SourceInfoOneofCase.Dfd)
      {
        documents[key].DfdSource = d.Dfd;
      }
    }

    public IEnumerable<AggregatedDocument> GetAggregatedDocuments()
    {
      return documents.Values;
    }

  }
}
