using System;
using System.Threading.Tasks;
using Yngdieng.Protos;
using static Yngdieng.Protos.Query.Types;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

namespace Yngdieng.Backend.Services
{
  public class YngdiengService : Yngdieng.Protos.YngdiengService.YngdiengServiceBase
  {
    private readonly ILogger<YngdiengService> _logger;
    private readonly IIndexHolder _indexHolder;

    public YngdiengService(ILogger<YngdiengService> logger, IIndexHolder indexHolder)
    {
      _logger = logger;
      _indexHolder = indexHolder;
    }

    public override Task<SearchResponse> GetSearch(SearchRequest request, ServerCallContext context)
    {
      _logger.LogInformation("A search request" + request.ToString());

      var query = QueryParser.Parse(request.Query) ?? throw new Exception("Invalid query");

      switch (query.QueryCase)
      {
        case Query.QueryOneofCase.PhonologyQuery:
          {
            var response = new SearchResponse();
            if (query.GroupBy == GroupByMethod.HanziPhonology)
            {
              response.Results.AddRange(GetPhonologyQueryAggregated(query.PhonologyQuery, query.SortBy).Select(a =>
               new SearchResultRow
               {
                 AggregatedDocument = a
               }
              ));
            }
            else
            {
              response.Results.AddRange(GetPhonologyQuery(query.PhonologyQuery, query.SortBy).Select(d =>
               new SearchResultRow
               {
                 Document = d
               }
              ));
            }
            return Task.FromResult(response);
          }
        case Query.QueryOneofCase.HanziQuery:
          {
            string hanziQuery = query.HanziQuery;
            if (hanziQuery == null)
            {
              throw new Exception("Cannot all be null");
            }
            var response = new SearchResponse();
            if (query.GroupBy == GroupByMethod.HanziPhonology)
            {
              response.Results.AddRange(GetHanziQueryAggregated(query.HanziQuery, query.SortBy).Select(a =>
              new SearchResultRow
              {
                AggregatedDocument = a
              }));
            }
            else
            {
              response.Results.AddRange(GetHanziQuery(query.HanziQuery, query.SortBy).Select(d =>
              new SearchResultRow
              {
                Document = d
              }));
            }

            return Task.FromResult(response);
          }
        default:
          throw new Exception("Not implemented");
      }
    }

    private IEnumerable<Document> GetPhonologyQuery(PhonologyQuery query, SortByMethod sortBy)
    {
      Initial initial = query.Initial;
      Final final = query.Final;
      Tone tone = query.Tone;
      if (initial == Initial.Unspecified &&
          final == Final.Unspecified &&
          tone == Tone.Unspecified)
      {
        throw new Exception("Cannot all be unspecified");
      }
      // Filter
      var documents = _indexHolder.GetIndex().Documents.Where(_ => true);
      if (initial != Initial.Unspecified)
      {
        documents = documents.Where(d => d.Initial == initial);
      }
      if (final != Final.Unspecified)
      {
        documents = documents.Where(d => d.Final == final);
      }
      if (tone != Tone.Unspecified)
      {
        documents = documents.Where(d => d.Tone == tone);
      }
      var matchedDocuments = documents;
      // Sort
      IEnumerable<Document> sorted;
      switch (sortBy)
      {
        case SortByMethod.InitialFinalTone:
          sorted = matchedDocuments.OrderBy(d => d.Final)
              .ThenBy(d => d.Initial)
              .ThenBy(d => d.Tone);
          break;
        case SortByMethod.SortByUnspecified:
        default:
          sorted = matchedDocuments;
          break;
      }
      return sorted;
    }

    private IEnumerable<AggregatedDocument> GetPhonologyQueryAggregated(PhonologyQuery query, SortByMethod sortBy)
    {
      Initial initial = query.Initial;
      Final final = query.Final;
      Tone tone = query.Tone;
      if (initial == Initial.Unspecified &&
          final == Final.Unspecified &&
          tone == Tone.Unspecified)
      {
        throw new Exception("Cannot all be unspecified");
      }
      // Filter
      var documents = _indexHolder.GetIndex().AggregatedDocument.Where(_ => true);
      if (initial != Initial.Unspecified)
      {
        documents = documents.Where(d => d.Initial == initial);
      }
      if (final != Final.Unspecified)
      {
        documents = documents.Where(d => d.Final == final);
      }
      if (tone != Tone.Unspecified)
      {
        documents = documents.Where(d => d.Tone == tone);
      }
      var matchedDocuments = documents;
      // Sort
      IEnumerable<AggregatedDocument> sorted;
      switch (sortBy)
      {
        case SortByMethod.InitialFinalTone:
          sorted = matchedDocuments.OrderBy(d => d.Final)
              .ThenBy(d => d.Initial)
              .ThenBy(d => d.Tone);
          break;
        case SortByMethod.SortByUnspecified:
        default:
          sorted = matchedDocuments;
          break;
      }
      return sorted;
    }

    private IEnumerable<Document> GetHanziQuery(string query, SortByMethod sortBy)
    {
      var matchedDocuments = _indexHolder.GetIndex().Documents
                .Where(d =>
                    GetHanzi(d.HanziCanonical) == query
                    || d.HanziAlternatives.Where(
                        r => GetHanzi(r) == query).Count() > 0
                    || d.HanziMatchable.IndexOf(query) >= 0
                );
      IEnumerable<Document> sorted;
      switch (sortBy)
      {
        case SortByMethod.InitialFinalTone:
          sorted = matchedDocuments.OrderBy(d => d.Final)
                      .ThenBy(d => d.Initial)
                      .ThenBy(d => d.Tone);
          break;
        case SortByMethod.SortByUnspecified:
        default:
          sorted = matchedDocuments;
          break;
      }
      return sorted;
    }

    private IEnumerable<AggregatedDocument> GetHanziQueryAggregated(string query, SortByMethod sortBy)
    {
      var matchedDocuments = _indexHolder.GetIndex().AggregatedDocument
                .Where(d =>
                    GetHanzi(d.HanziCanonical) == query
                    || d.HanziAlternatives.Where(
                        r => GetHanzi(r) == query).Count() > 0
                    || d.HanziMatchable.IndexOf(query) >= 0
                );
      IEnumerable<AggregatedDocument> sorted;
      switch (sortBy)
      {
        case SortByMethod.InitialFinalTone:
          sorted = matchedDocuments.OrderBy(d => d.Final)
                      .ThenBy(d => d.Initial)
                      .ThenBy(d => d.Tone);
          break;
        case SortByMethod.SortByUnspecified:
        default:
          sorted = matchedDocuments;
          break;
      }
      return sorted;
    }

    public static string GetHanzi(Hanzi h)
    {
      return h.HanziCase == Hanzi.HanziOneofCase.Regular
                  ? h.Regular
                  : h.Ids;
    }
    public override Task<GetDocumentResponse> GetDocument(GetDocumentRequest request, ServerCallContext context)
    {
      _logger.LogInformation("GetDocumentRequest: " + request.ToString());
      switch (request.DocumentIdCase)
      {
        case GetDocumentRequest.DocumentIdOneofCase.CiklinId:
          {
            var ciklinId = request.CiklinId;
            var matchedDocument = _indexHolder
                .GetIndex()
                .Documents
                .Where(document =>
                    (document as Document).CiklinId == ciklinId)
                .FirstOrDefault();
            return Task.FromResult(new GetDocumentResponse()
            {
              Document = matchedDocument
            });
          }
        case GetDocumentRequest.DocumentIdOneofCase.DfdId:
          {
            var dfdId = request.DfdId;
            var matchedDocument = _indexHolder.GetIndex().Documents.Where(document => (document as Document).DfdId == dfdId).FirstOrDefault();
            return Task.FromResult(new GetDocumentResponse()
            {
              Document = matchedDocument
            });
          }
        default:
          throw new Exception("Bad request");
      }
      return Task.FromResult(new GetDocumentResponse());
    }
  }
}