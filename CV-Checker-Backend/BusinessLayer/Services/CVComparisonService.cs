using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BusinessLogic.DTOs;
using Domain.Entities;
using BusinessLogic.Interface;
using DAL.Interface;

namespace BusinessLogic.Services
{
    public class CVComparisonService : ICVComparisonService
    {
        private readonly ICVComparisonRepository _comparisonRepository;
        private readonly ICVRepository _cvRepository;
        private readonly IJobOfferRepository _jobOfferRepository;
        private readonly IUserRepository _userRepository;

        public CVComparisonService(
            ICVComparisonRepository comparisonRepository,
            ICVRepository cvRepository,
            IJobOfferRepository jobOfferRepository,
            IUserRepository userRepository)
        {
            _comparisonRepository = comparisonRepository;
            _cvRepository = cvRepository;
            _jobOfferRepository = jobOfferRepository;
            _userRepository = userRepository;
        }

        public Task<CVComparison?> GetByIdAsync(Guid id)
        {
            return _comparisonRepository.GetByIdAsync(id);
        }

        public Task<IEnumerable<CVComparison>> GetByUserIdAsync(Guid userId)
        {
            return _comparisonRepository.GetByUserIdAsync(userId);
        }

        public async Task<CVComparison> CreateCVComparisonAsync(CVComparison comparison)
        {
            if (comparison.CVId == Guid.Empty)
                throw new ArgumentException("CVId is required.");

            if (comparison.JobOfferId == Guid.Empty)
                throw new ArgumentException("JobOfferId is required.");

            if (comparison.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required.");

            // Verify CV exists
            var cv = await _cvRepository.GetByIdAsync(comparison.CVId);
            if (cv == null)
                throw new ArgumentException("CV not found.");

            // Verify JobOffer exists
            var jobOffer = await _jobOfferRepository.GetByIdAsync(comparison.JobOfferId);
            if (jobOffer == null)
                throw new ArgumentException("JobOffer not found.");

            // Verify User exists
            var user = await _userRepository.GetByIdAsync(comparison.UserId);
            if (user == null)
                throw new ArgumentException("User not found.");

            // Ensure UserId matches CV's UserId
            if (cv.UserId != comparison.UserId)
                throw new ArgumentException("CV does not belong to the specified user.");

            if (comparison.CreatedAt == default)
                comparison.CreatedAt = DateTime.UtcNow;

            return await _comparisonRepository.CreateCVComparisonAsync(comparison);
        }

        public async Task<CVComparison> CreateAutoCVComparisonAsync(CreateAutoCVComparisonDTO dto)
        {
            if (dto.CVId == Guid.Empty)
                throw new ArgumentException("CVId is required.");

            if (dto.JobOfferId == Guid.Empty)
                throw new ArgumentException("JobOfferId is required.");

            if (dto.UserId == Guid.Empty)
                throw new ArgumentException("UserId is required.");

            var cv = await _cvRepository.GetByIdAsync(dto.CVId);
            if (cv == null)
                throw new ArgumentException("CV not found.");

            var jobOffer = await _jobOfferRepository.GetByIdAsync(dto.JobOfferId);
            if (jobOffer == null)
                throw new ArgumentException("JobOffer not found.");

            var user = await _userRepository.GetByIdAsync(dto.UserId);
            if (user == null)
                throw new ArgumentException("User not found.");

            if (cv.UserId != dto.UserId)
                throw new ArgumentException("CV does not belong to the specified user.");

            var cvText = (cv.Content ?? string.Empty).Trim();
            var jobText = BuildJobOfferText(jobOffer);

            if (string.IsNullOrWhiteSpace(cvText))
                throw new ArgumentException("CV content is empty. Upload a PDF first or provide parsed text.");

            if (string.IsNullOrWhiteSpace(jobText))
                throw new ArgumentException("Job offer text is empty. Fill description/requirements first.");

            var cvKeywords = ExtractKeywords(cvText);
            var jobKeywords = ExtractKeywords(jobText);
            var matched = jobKeywords.Intersect(cvKeywords, StringComparer.OrdinalIgnoreCase).ToList();
            var missing = jobKeywords.Except(cvKeywords, StringComparer.OrdinalIgnoreCase).ToList();

            var score = CalculateMatchScore(jobKeywords.Count, matched.Count);

            var strengths = matched.Any()
                ? $"Matched keywords: {string.Join(", ", matched.Take(12))}"
                : "No strong keyword matches were detected.";

            var weaknesses = missing.Any()
                ? $"Missing keywords: {string.Join(", ", missing.Take(12))}"
                : "No critical missing keywords were detected.";

            var suggestions = missing.Any()
                ? $"Add or highlight evidence of these skills/terms in your CV: {string.Join(", ", missing.Take(8))}."
                : "Improve quantified achievements and tailor the CV title/summary to the role.";

            var analysis = $"Auto-analysis based on semantic keyword overlap between CV and job offer text. " +
                           $"Matched {matched.Count} of {jobKeywords.Count} relevant keywords.";

            var comparison = new CVComparison
            {
                Id = Guid.NewGuid(),
                CVId = dto.CVId,
                JobOfferId = dto.JobOfferId,
                UserId = dto.UserId,
                MatchScore = score,
                Strengths = strengths,
                Weaknesses = weaknesses,
                Suggestions = suggestions,
                AnalysisResult = analysis,
                CreatedAt = DateTime.UtcNow
            };

            return await _comparisonRepository.CreateCVComparisonAsync(comparison);
        }

        private static string BuildJobOfferText(JobOffer jobOffer)
        {
            var primary = jobOffer.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(primary))
                return primary;

            return $"{jobOffer.Title} {jobOffer.Description} {jobOffer.Requirements}".Trim();
        }

        private static List<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "and", "for", "with", "from", "that", "this", "your", "you", "are", "will",
                "a", "an", "of", "to", "in", "on", "at", "as", "or", "be", "is", "it", "by", "we",
                "our", "their"
            };

            var words = Regex.Matches(text.ToLowerInvariant(), @"[a-zA-ZÀ-ÿ0-9\+\#\.]{2,}")
                .Select(m => m.Value)
                .Where(w => !stopWords.Contains(w))
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(80)
                .ToList();

            return words;
        }

        private static int CalculateMatchScore(int totalJobKeywords, int matchedKeywords)
        {
            if (totalJobKeywords <= 0)
                return 0;

            var ratio = (double)matchedKeywords / totalJobKeywords;
            var score = (int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero);

            return Math.Max(0, Math.Min(100, score));
        }
    }
}

