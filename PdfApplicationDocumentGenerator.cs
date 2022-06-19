using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext _dataContext;
		private readonly IPathProvider _templatePathProvider;
		private readonly IViewGenerator _viewGenerator;
		private readonly IConfiguration _configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
		private readonly IPdfGenerator _pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			_dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
			_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException(nameof(templatePathProvider));
			_viewGenerator = viewGenerator ?? throw new ArgumentNullException(nameof(viewGenerator));
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
		}
		
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			Application application = _dataContext.Applications.Single(app => app.Id == applicationId);

			if (application != null)
			{

				if (baseUri.EndsWith("/"))
					baseUri = baseUri.Substring(baseUri.Length - 1);

				string view = GetView(application, baseUri);

				var pdfOptions = new PdfOptions
				{
					PageNumbers = PageNumbers.Numeric,
					HeaderOptions = new HeaderOptions
					{
						HeaderRepeat = HeaderRepeat.FirstPageOnly,
						HeaderHtml = PdfConstants.Header
					}
				};
				var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
				return pdf.ToBytes();
			}
			else
			{
				
				_logger.LogWarning(
					$"No application found for id '{applicationId}'");
				return null;
			}
		}

		private string GetView(Application application, string baseUri)
		{
			string path = "";
			switch (application.State)
			{
				case ApplicationState.Pending:
					{
						path = _templatePathProvider.Get("PendingApplication");
						var vm = GetPendingApplicationViewModel(application);
						return _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), vm);
					}
				case ApplicationState.Activated:
					{
						path = _templatePathProvider.Get("ActivatedApplication");
						var vm = GetActivatedApplicationViewModel(application);
						return _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), vm);
					}
				case ApplicationState.InReview:
					{
						path = _templatePathProvider.Get("InReviewApplication");
						var vm = GetInReviewApplicationViewModel(application);
						return _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), vm);
					}
				default:
					{
						_logger.LogWarning(
						 $"The application is in state '{application.State}' and no valid document can be generated for it.");
						return null;
					}
			}
		}

		private PendingApplicationViewModel GetPendingApplicationViewModel(Application application)
		{
			//Validate
			if (application.Person == null)
			{
				_logger.LogWarning($"The application model is invalid.Person Entity Is Null");
				throw new ArgumentNullException(nameof(application.Person));
			}
			PendingApplicationViewModel vm = new PendingApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = string.Format("{0} {1}", application.Person.FirstName, application.Person.Surname),
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature
			};

			return vm;
		}

		private ActivatedApplicationViewModel GetActivatedApplicationViewModel(Application application)
		{
			//Validate
			if (application.Person == null)
			{
				_logger.LogWarning($"The application model is invalid.Person Entity Is Null");
				throw new ArgumentNullException(nameof(application.Person));
			}
			if (application.IsLegalEntity && application.LegalEntity == null)
			{
				_logger.LogWarning($"The application model is invalid.Legal Entity Is Null");
				throw new ArgumentNullException(nameof(application.LegalEntity));
			}


			ActivatedApplicationViewModel vm = new ActivatedApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = string.Format("{0} {1}", application.Person.FirstName, application.Person.Surname),
				LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products.SelectMany(p => p.Funds),
				PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
												   .Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
												   .Sum(),
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature
			};

			return vm;
		}

		private InReviewApplicationViewModel GetInReviewApplicationViewModel(Application application)
		{
			//Validate the Application Model State
			if (application.Person == null)
			{
				_logger.LogWarning($"The application model is invalid.Person Entity Is Null");
				throw new ArgumentNullException(nameof(application.Person));
			}
			if (application.IsLegalEntity && application.LegalEntity == null)
			{
				_logger.LogWarning($"The application model is invalid.Legal Entity Is Null");
				throw new ArgumentNullException(nameof(application.LegalEntity));
			}
			if (application.CurrentReview == null)
			{
				_logger.LogWarning($"The application model is invalid.CurrentReview Entity Is Null");
				throw new ArgumentNullException(nameof(application.CurrentReview));
			}

			var inReviewMessage = "Your application has been placed in review" +
								   application.CurrentReview.Reason switch
								   {
									   { } reason when reason.Contains("address") =>
										   " pending outstanding address verification for FICA purposes.",
									   { } reason when reason.Contains("bank") =>
										   " pending outstanding bank account verification.",
									   _ =>
										   " because of suspicious account behaviour. Please contact support ASAP."
								   };
			InReviewApplicationViewModel vm = new InReviewApplicationViewModel()
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = string.Format("{0} {1}", application.Person.FirstName, application.Person.Surname),
				LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products.SelectMany(p => p.Funds),
				PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
				.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
				.Sum(),
				InReviewMessage = inReviewMessage,
				InReviewInformation = application.CurrentReview,
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature
			};

			return vm;
		}
	}
}
