# Yunu.Commerce
Enterprise-grade GenAI commerce platform built with .NET, DDD, Clean Architecture, Hexagonal Architecture, and Event-Driven Architecture.

## Catalog Domain Modeling Notes

- `Product.Description` is a plain optional `string?` property, not a Value Object. Introduce a dedicated Value Object only when real validation/business rules justify it (docs/adr, YAGNI).
