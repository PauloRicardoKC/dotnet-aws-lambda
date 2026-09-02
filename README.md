# dotnet-aws-lambda

## Project overview

A small .NET 10 learning project that processes Amazon S3 `ObjectCreated` events with AWS Lambda. It reads object metadata, writes structured logs to CloudWatch, and can move each file to a processed prefix by copying it and then deleting the original.

The project intentionally has no database, API Gateway, SQS, SNS, or EventBridge integration.

## Architecture

```text
incoming/document.pdf
        ↓
Amazon S3
        ↓
ObjectCreated
        ↓
AWS Lambda
        ↓
FileProcessor
        ↓
processed/document.pdf
```

The Lambda handler handles AWS-specific input and delegates the use case to the Application project. `IStorageProvider` keeps the Application project independent of the AWS SDK. Its S3 implementation remains inside the Function project.

## Projects

- `FileProcessor.Application`: processing rules, typed options, models, and abstractions.
- `FileProcessor.Function`: Lambda handler, dependency injection, AWS SDK integration, and `AwsS3StorageProvider`.
- `FileProcessor.UnitTests`: isolated xUnit tests using FluentAssertions and an in-memory storage fake.

## Processing flow

For every S3 event record, the handler decodes the URL-encoded object key and creates a `FileProcessRequest` containing the bucket, key, size, and event time.

The processor then:

1. Validates the request.
2. Skips keys already under the configured processed prefix.
3. Retrieves object metadata from S3.
4. Copies the object to the processed prefix when moving is enabled.
5. Deletes the original only after a successful copy.
6. Returns a `FileProcessResult` and writes structured logs.

Exceptions are logged and rethrown. This marks the Lambda invocation as failed and preserves the expected AWS asynchronous retry behavior. A failed copy never causes the original object to be deleted.

## Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `PROCESSED_PREFIX` | `processed/` | Destination prefix and loop-protection prefix. |
| `MOVE_FILES` | `true` | When `true`, copies the file and deletes the original. When `false`, only validates, reads metadata, and logs the file. |

These values populate the typed `FileProcessingOptions` configuration used through `IOptions<FileProcessingOptions>`.

## AWS authentication

The Lambda does not use an Access Key or Secret Key in source code or configuration. In AWS, the SDK automatically obtains temporary credentials from the Lambda IAM Execution Role.

Local credentials, such as an AWS CLI profile, AWS IAM Identity Center session, or environment credentials, are only for local AWS tooling. They are separate from the credentials supplied to the deployed Lambda.

## IAM Execution Role

Attach the managed `AWSLambdaBasicExecutionRole` policy for CloudWatch Logs access. Restrict S3 access to the bucket used by this function and grant these minimum object permissions:

- `s3:GetObject`
- `s3:PutObject`
- `s3:DeleteObject`

Example S3 policy statement:

```json
{
  "Effect": "Allow",
  "Action": [
    "s3:GetObject",
    "s3:PutObject",
    "s3:DeleteObject"
  ],
  "Resource": "arn:aws:s3:::YOUR_BUCKET/*"
}
```

The role trust policy must allow the `lambda.amazonaws.com` service principal.

## S3 trigger

Configure an S3 notification for the Lambda with:

- Event: `s3:ObjectCreated:*`
- Prefix: `incoming/`

With this filter, `incoming/file.pdf` invokes the Lambda, while the copied `processed/file.pdf` does not create another invocation. The processor still checks the processed prefix in code as an additional defense against loops or a broader trigger configuration.

The S3 bucket and Lambda must be in the same AWS Region. When the trigger is created in the AWS console, the console normally adds the resource-based permission that allows S3 to invoke the function.

## CloudWatch logs

Native `ILogger` structured logs are written to standard output and collected by Lambda in the CloudWatch Logs group:

```text
/aws/lambda/YOUR_FUNCTION_NAME
```

Messages include the AWS request ID, bucket, source key, destination key, processing stages, skips, warnings, successes, and failures.

## Build

```bash
dotnet restore DotnetAwsLambda.slnx
dotnet build DotnetAwsLambda.slnx --no-restore
```

## Tests

```bash
dotnet test DotnetAwsLambda.slnx --no-build --no-restore
```

The tests do not connect to AWS. They cover normal processing, loop protection, destination keys, operation ordering, copy failures, exception propagation, required arguments, and configurable prefixes.

## Deploy

Install the AWS Lambda .NET CLI tool if needed, then create the deployment package:

```bash
dotnet tool install -g Amazon.Lambda.Tools
dotnet lambda package --project-location src/FileProcessor.Function --configuration Release --framework net10.0 --output-package publish/FileProcessor.Function.zip
```

Create a .NET 10 Lambda function, attach its IAM Execution Role, upload the ZIP, and use this handler:

```text
FileProcessor.Function::FileProcessor.Function.Function::FunctionHandler
```

Configure `PROCESSED_PREFIX` and `MOVE_FILES` in the Lambda environment variables, then add the recommended S3 trigger.

## Testing

Upload a file to the incoming prefix:

```bash
aws s3 cp document.pdf s3://YOUR_BUCKET/incoming/document.pdf
```

With `MOVE_FILES=true`, confirm that `processed/document.pdf` exists, the original is gone, and the structured processing messages appear in CloudWatch Logs.

## Key concepts

- S3 event-driven Lambda invocation.
- URL decoding of S3 object keys.
- Small handler and dependency injection.
- Application code independent of AWS SDK types.
- Typed options populated from environment configuration.
- Temporary credentials supplied by an IAM Execution Role.
- Copy-before-delete semantics to protect the original on failure.
- Code-level loop protection in addition to the S3 prefix filter.
- Rethrown exceptions for correct Lambda failure and retry behavior.
