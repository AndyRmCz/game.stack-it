-- Metric views (docs/PLAN.md, "Measurement findings", section I). Days are UTC.
-- Apply / refresh: uv run python -m scripts.metrics apply
-- Statements are separated by a semicolon at the end of a line; keep it that way.

CREATE SCHEMA IF NOT EXISTS metrics;

-- A player is active on a day with a session start (telemetry) or a run (server).
CREATE OR REPLACE VIEW metrics.activity AS
SELECT user_id, (occurred_at AT TIME ZONE 'UTC')::date AS day
FROM client_events
WHERE name = 'session_start'
UNION
SELECT user_id, (started_at AT TIME ZONE 'UTC')::date AS day
FROM runs;

CREATE OR REPLACE VIEW metrics.dau AS
SELECT day, count(*) AS dau
FROM metrics.activity
GROUP BY day;

-- DN retention: share of a day's new players active exactly N days later. Rows for cohorts
-- younger than N days are not final yet (see days_old).
CREATE OR REPLACE VIEW metrics.retention AS
WITH cohort AS (
    SELECT id AS user_id, (created_at AT TIME ZONE 'UTC')::date AS cohort_day
    FROM users
),
counted AS (
    SELECT
        c.cohort_day,
        count(DISTINCT c.user_id) AS installs,
        count(DISTINCT a.user_id) FILTER (WHERE a.day = c.cohort_day + 1) AS d1_users,
        count(DISTINCT a.user_id) FILTER (WHERE a.day = c.cohort_day + 7) AS d7_users,
        count(DISTINCT a.user_id) FILTER (WHERE a.day = c.cohort_day + 30) AS d30_users
    FROM cohort c
    LEFT JOIN metrics.activity a ON a.user_id = c.user_id
    GROUP BY c.cohort_day
)
SELECT
    cohort_day,
    (now() AT TIME ZONE 'UTC')::date - cohort_day AS days_old,
    installs,
    d1_users,
    round(100.0 * d1_users / installs, 1) AS d1_pct,
    d7_users,
    round(100.0 * d7_users / installs, 1) AS d7_pct,
    d30_users,
    round(100.0 * d30_users / installs, 1) AS d30_pct
FROM counted;

-- One row per app session. Length is the reported foreground time when the session ended
-- cleanly, else the span between its first and last event.
CREATE OR REPLACE VIEW metrics.sessions AS
SELECT
    user_id,
    session_id,
    min(occurred_at) AS started_at,
    coalesce(
        max((props ->> 'duration_s')::numeric) FILTER (WHERE name = 'session_end'),
        extract(epoch FROM max(occurred_at) - min(occurred_at))
    ) AS duration_s,
    count(*) FILTER (WHERE name = 'run_start') AS runs,
    count(*) FILTER (WHERE name = 'ad_shown') AS ads,
    count(*) FILTER (WHERE name = 'ad_shown' AND props ->> 'format' = 'interstitial')
        AS interstitials,
    count(*) FILTER (WHERE name = 'ad_shown' AND props ->> 'format' = 'rewarded') AS rewarded,
    bool_or(name = 'session_end') AS ended
FROM client_events
GROUP BY user_id, session_id;

CREATE OR REPLACE VIEW metrics.session_daily AS
SELECT
    (started_at AT TIME ZONE 'UTC')::date AS day,
    count(*) AS sessions,
    count(DISTINCT user_id) AS users,
    round(count(*)::numeric / count(DISTINCT user_id), 2) AS sessions_per_user,
    round(percentile_cont(0.5) WITHIN GROUP (ORDER BY duration_s)::numeric, 1)
        AS median_session_s,
    round(avg(runs), 2) AS runs_per_session,
    round(avg(ads), 2) AS ads_per_session,
    round(avg(interstitials), 2) AS interstitials_per_session,
    round(avg(rewarded), 2) AS rewarded_per_session
FROM metrics.sessions
GROUP BY 1;

-- Fill rate = loaded / requested; CTR = clicked / shown.
CREATE OR REPLACE VIEW metrics.ads_daily AS
WITH counted AS (
    SELECT
        (occurred_at AT TIME ZONE 'UTC')::date AS day,
        props ->> 'format' AS format,
        count(*) FILTER (WHERE name = 'ad_requested') AS requested,
        count(*) FILTER (WHERE name = 'ad_loaded') AS loaded,
        count(*) FILTER (WHERE name = 'ad_load_failed') AS load_failed,
        count(*) FILTER (WHERE name = 'ad_shown') AS shown,
        count(*) FILTER (WHERE name = 'ad_show_failed') AS show_failed,
        count(*) FILTER (WHERE name = 'ad_clicked') AS clicked,
        count(*) FILTER (WHERE name = 'ad_rewarded') AS rewarded
    FROM client_events
    WHERE name LIKE 'ad\_%'
    GROUP BY 1, 2
)
SELECT
    *,
    round(100.0 * loaded / nullif(requested, 0), 1) AS fill_pct,
    round(100.0 * clicked / nullif(shown, 0), 2) AS ctr_pct
FROM counted;

-- Rewarded offers: opt-in = accepted / shown; completion = rewards earned / offers accepted.
CREATE OR REPLACE VIEW metrics.offers_daily AS
WITH counted AS (
    SELECT
        (occurred_at AT TIME ZONE 'UTC')::date AS day,
        props ->> 'placement' AS placement,
        count(*) FILTER (WHERE name = 'offer_shown') AS shown,
        count(*) FILTER (WHERE name = 'offer_accepted') AS accepted,
        count(*) FILTER (WHERE name = 'offer_declined') AS declined,
        count(*) FILTER (WHERE name = 'ad_rewarded') AS rewards_earned
    FROM client_events
    WHERE name IN ('offer_shown', 'offer_accepted', 'offer_declined', 'ad_rewarded')
    GROUP BY 1, 2
)
SELECT
    *,
    round(100.0 * accepted / nullif(shown, 0), 1) AS opt_in_pct,
    round(100.0 * rewards_earned / nullif(accepted, 0), 1) AS completion_pct
FROM counted;

-- Estimated ad revenue from impression-level OnAdPaid values. AdMob's console is the source
-- of truth for payouts; reconcile monthly.
CREATE OR REPLACE VIEW metrics.revenue_daily AS
WITH paid AS (
    SELECT
        (occurred_at AT TIME ZONE 'UTC')::date AS day,
        props ->> 'currency' AS currency,
        props ->> 'format' AS format,
        (props ->> 'value_micros')::numeric AS value_micros
    FROM client_events
    WHERE name = 'ad_paid'
)
SELECT
    p.day,
    p.currency,
    count(*) AS paid_impressions,
    round(sum(p.value_micros) / 1000000, 6) AS revenue,
    round(sum(p.value_micros) FILTER (WHERE p.format = 'interstitial') / 1000000, 6) AS interstitial,
    round(sum(p.value_micros) FILTER (WHERE p.format = 'rewarded') / 1000000, 6) AS rewarded,
    round(sum(p.value_micros) FILTER (WHERE p.format = 'banner') / 1000000, 6) AS banner,
    d.dau,
    round(sum(p.value_micros) / 1000000 / d.dau, 5) AS arpdau
FROM paid p
JOIN metrics.dau d ON d.day = p.day
GROUP BY p.day, p.currency, d.dau;

-- Last screen seen in each finished session: where players leave.
CREATE OR REPLACE VIEW metrics.quit_points AS
SELECT screen, count(*) AS sessions
FROM (
    SELECT DISTINCT ON (e.user_id, e.session_id) e.props ->> 'screen' AS screen
    FROM client_events e
    JOIN metrics.sessions s ON s.user_id = e.user_id AND s.session_id = e.session_id
    WHERE e.name = 'screen_view' AND s.ended
    ORDER BY e.user_id, e.session_id, e.occurred_at DESC
) last_screen
GROUP BY screen;

CREATE OR REPLACE VIEW metrics.runs_daily AS
SELECT
    (started_at AT TIME ZONE 'UTC')::date AS day,
    count(*) AS runs,
    count(*) FILTER (WHERE status = 'verified') AS verified,
    count(*) FILTER (WHERE status = 'started') AS unfinished,
    count(*) FILTER (WHERE status IN ('flagged', 'rejected')) AS flagged_or_rejected,
    percentile_disc(0.5) WITHIN GROUP (ORDER BY verified_score)
        FILTER (WHERE status = 'verified') AS median_score,
    round(avg(extract(epoch FROM finished_at - started_at))
        FILTER (WHERE status = 'verified'), 1) AS avg_run_s,
    round(sum(perfects)::numeric / nullif(sum(placed), 0), 3) AS perfect_rate,
    round(100.0 * count(*) FILTER (WHERE continues_used > 0)
        / nullif(count(*) FILTER (WHERE status = 'verified'), 0), 1) AS continued_pct,
    round(100.0 * count(*) FILTER (WHERE coins_doubled)
        / nullif(count(*) FILTER (WHERE status = 'verified'), 0), 1) AS doubled_pct,
    count(*) FILTER (WHERE challenge_id IS NOT NULL) AS duel_runs
FROM runs
GROUP BY 1;

-- Each player's tap accuracy: perfects / placed maps onto the timing sigma of
-- scripts/economy_model.py (0.43 ~ 25 ms, 0.33 ~ 35 ms, 0.25 ~ 50 ms, 0.18 ~ 70 ms).
CREATE OR REPLACE VIEW metrics.player_accuracy AS
SELECT
    user_id,
    count(*) AS verified_runs,
    round(sum(perfects)::numeric / nullif(sum(placed), 0), 3) AS perfect_rate,
    round(avg(coins_earned), 2) AS coins_per_run
FROM runs
WHERE status = 'verified'
GROUP BY user_id;

CREATE OR REPLACE VIEW metrics.economy_daily AS
SELECT
    (created_at AT TIME ZONE 'UTC')::date AS day,
    kind,
    count(*) AS transactions,
    count(DISTINCT user_id) AS users,
    sum(amount) AS coins
FROM wallet_transactions
GROUP BY 1, 2;

CREATE OR REPLACE VIEW metrics.first_purchase AS
SELECT
    u.id AS user_id,
    u.created_at,
    min(t.created_at) AS first_purchase_at,
    round(extract(epoch FROM min(t.created_at) - u.created_at) / 3600, 1)
        AS hours_to_first_purchase
FROM users u
LEFT JOIN wallet_transactions t ON t.user_id = u.id AND t.kind = 'purchase'
GROUP BY u.id, u.created_at;

CREATE OR REPLACE VIEW metrics.item_sales AS
SELECT s.id AS item_id, s.kind, s.price, count(us.user_id) AS owners
FROM skins s
LEFT JOIN user_skins us ON us.skin_id = s.id
WHERE NOT s.is_default
GROUP BY s.id, s.kind, s.price;
