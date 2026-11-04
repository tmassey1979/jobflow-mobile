alter table public.private_kit_handoffs
  add column if not exists metadata jsonb not null default '{}'::jsonb;
alter table public.private_kit_handoffs
  add constraint private_kit_handoffs_metadata_object
  check (jsonb_typeof(metadata) = 'object');