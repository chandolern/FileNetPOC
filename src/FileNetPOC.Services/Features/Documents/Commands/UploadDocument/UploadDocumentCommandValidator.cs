using FluentValidation;

namespace FileNetPOC.Services.Features.Documents.Commands.UploadDocument;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required.");
            
        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.");
            
        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("Author is required.");
            
        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File cannot be empty.")
            // Limiting to 25MB for the POC
            .LessThanOrEqualTo(25 * 1024 * 1024).WithMessage("File size must not exceed 25 MB."); 
            
        RuleFor(x => x.FileContent)
            .NotNull().NotEmpty().WithMessage("File content is required.");
    }
}