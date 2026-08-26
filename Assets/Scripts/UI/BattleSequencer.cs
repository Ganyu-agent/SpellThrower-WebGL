using System.Collections;
using UnityEngine;

namespace SpellThrower
{
    /// 서버는 결과 상태만 한 번에 보낸다. 그대로 그리면 이동·공격·피해가 동시에 튀므로
    /// 이전 상태와 비교해 "이동 → 공격 → 피해" 순서로 나눠 재생한다.
    /// 연출이 끝나기 전까지 _shown 을 유지해 GameUI 가 옛 위치를 그리게 한다.
    public class BattleSequencer : MonoBehaviour
    {
        [Header("연출 길이 — 실기 화면 보고 맞춘다")]
        public float moveSeconds = 0.28f;
        public float attackWindup = 0.22f;
        public float damageHold = 0.35f;

        GameUI _ui;
        BattleFx _fx;
        SPUM_Prefabs _selfAnim, _foeAnim;
        SfxPlayer _sfx;
        Transform _selfTf, _foeTf;

        GameState _shown;
        bool _hasShown, _busy;

        /// 이동 보간 중인 말만 상태 좌표 대신 이 위치를 쓴다. 플래그를 한쪽으로 묶으면
        /// 가만히 있는 말이 남아 있던 옛 좌표로 튄다.
        /// 연출이 다 끝날 때까지 유지한다. 보간이 끝나자마자 풀면 _shown 이 아직 옛 상태라
        /// 밀려난 말이 원래 칸으로 되돌아갔다가 다시 튀어 나간다.
        Vector3 _selfPos, _foePos;
        bool _overrideSelf, _overrideFoe;

        public bool Busy => _busy;

        public void Init(GameUI ui, BattleFx fx, Transform self, Transform foe, SfxPlayer sfx = null)
        {
            _ui = ui; _fx = fx; _sfx = sfx; _selfTf = self; _foeTf = foe;
            _selfAnim = Prepare(self);
            _foeAnim = Prepare(foe);
        }

        /// 맵(0~2)·칸 표시 보다 말이 항상 위에 오도록 파츠 정렬을 통째로 올린다.
        /// 파츠끼리의 앞뒤 관계는 그대로 유지된다. 장판(20~30)보다는 위, 폭발(500~)보다는 아래다.
        const int CharacterLift = 200;

        /// SPUM 유닛은 클립 목록을 직접 채워줘야 PlayAnimation 이 동작한다.
        static SPUM_Prefabs Prepare(Transform character)
        {
            if (character == null) return null;
            foreach (var sr in character.GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder += CharacterLift;
            // SPUM 유닛은 UnitRoot 에 SortingGroup 을 달고 있다. 그러면 위의 파츠 정렬값은
            // 유닛 안에서만 쓰이고, 다른 오브젝트와의 앞뒤는 그룹 정렬값 하나로 정해진다.
            // 그룹을 같이 올려야 장판·알갱이 위로 말이 올라온다.
            foreach (var group in character.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true))
                group.sortingOrder += CharacterLift;
            var spum = character.GetComponentInChildren<SPUM_Prefabs>(true);
            if (spum == null) return null;
            if (!spum.allListsHaveItemsExist()) spum.PopulateAnimationLists();
            spum.OverrideControllerInit();
            return spum;
        }

        static void Anim(SPUM_Prefabs spum, PlayerState state)
        {
            if (spum == null) return;
            spum.PlayAnimation(state, 0);
        }

        /// GameUI 가 매 프레임 부른다. 아직 연출 중이면 그 상태를 유지한다.
        public GameState Present(GameState live)
        {
            if (!_hasShown) { _shown = live; _hasShown = true; return _shown; }
            if (!_busy && Differs(_shown, live)) StartCoroutine(PlayTransition(_shown, live));
            return _shown;
        }

        /// 말 위치. 이동 보간 중이면 보간 위치를 준다.
        public Vector3 WorldOf(bool isSelf, int x, int y)
        {
            if (isSelf && _overrideSelf) return _selfPos;
            if (!isSelf && _overrideFoe) return _foePos;
            return _ui.TileWorld(x, y);
        }

        static bool Differs(GameState a, GameState b) =>
            a.p0X != b.p0X || a.p0Y != b.p0Y || a.p1X != b.p1X || a.p1Y != b.p1Y ||
            a.p0Hp != b.p0Hp || a.p1Hp != b.p1Hp ||
            a.turnCount != b.turnCount || a.turnPlayer != b.turnPlayer ||
            a.actionLeft != b.actionLeft || a.winner != b.winner ||
            a.p0Hand.Length != b.p0Hand.Length || a.p1Hand.Length != b.p1Hand.Length ||
            a.p0MoveLocked != b.p0MoveLocked || a.p1MoveLocked != b.p1MoveLocked ||
            a.lastActionSequence != b.lastActionSequence || WorldEffectsDiffer(a, b);

        static bool WorldEffectsDiffer(GameState a, GameState b)
        {
            if (a.worldEffects.Length != b.worldEffects.Length) return true;
            for (int i = 0; i < a.worldEffects.Length; i++)
            {
                var x = a.worldEffects[i];
                var y = b.worldEffects[i];
                if (x.Kind != y.Kind || x.Phase != y.Phase ||
                    x.SourcePlayer != y.SourcePlayer || x.TriggerPlayer != y.TriggerPlayer ||
                    x.TargetPlayer != y.TargetPlayer || x.X != y.X || x.Y != y.Y ||
                    x.RemainingTurns != y.RemainingTurns || x.Power != y.Power ||
                    x.Sequence != y.Sequence)
                    return true;
            }
            return false;
        }

        IEnumerator PlayTransition(GameState from, GameState to)
        {
            _busy = true;
            int me = NetGame.I != null ? NetGame.I.MyPlayer : 0;

            if (from.turnCount != to.turnCount || from.turnPlayer != to.turnPlayer)
                _sfx?.Play(SfxId.TurnStart);

            // 0) 카드 시전 — 투사체/빔은 명중할 때까지 기다렸다가 피해로 넘어간다.
            //    얼음 장판과 토템은 상태가 남아 있는 동안 GameUI 가 유지형 연출로 그린다.
            bool cast = to.lastActionSequence != from.lastActionSequence &&
                        to.lastActionKind == GameplayActionKind.CardUsed;
            int caster = to.lastActionPlayer;
            // 카드가 스스로 명중 연출을 낸 경우에만 공용 타격 이펙트를 생략한다.
            // 질주·토템·특수처럼 명중 연출이 없는 카드는 피해가 들어가도 아무것도 안 보였다.
            bool castImpact = false;
            if (cast)
            {
                var casterAnim = caster == me ? _selfAnim : _foeAnim;
                var at = _ui.TileWorld(to.lastActionTargetX, to.lastActionTargetY);
                var src = _ui.TileWorld(Px(from, caster), Py(from, caster));
                var used = Cards.Get(to.lastActionCardId);
                if (used != null) _sfx?.Play(used.SfxCue);
                switch (used != null ? used.Attribute : CardAttribute.Special)
                {
                    case CardAttribute.Fire:
                        Anim(casterAnim, PlayerState.ATTACK);
                        castImpact = true;
                        yield return _fx.Projectile(src, at);
                        break;
                    case CardAttribute.Wind:
                        Anim(casterAnim, PlayerState.ATTACK);
                        castImpact = true;
                        yield return _fx.Beam(src, at, used.Range);
                        break;
                    case CardAttribute.Lightning:
                        Anim(casterAnim, PlayerState.ATTACK);
                        castImpact = true;
                        _fx.PlayGround(FxKind.Thunder, at, 1.4f);
                        yield return new WaitForSeconds(attackWindup);
                        break;
                    case CardAttribute.Heal:
                        _fx.PlayGround(FxKind.HealCast, src);
                        break;
                    case CardAttribute.Sprint:
                        _fx.Dash(caster == me ? _selfTf : _foeTf, moveSeconds);
                        break;
                }
            }

            // 1) 이동 — 자리가 바뀐 말을 보간해서 옮긴다
            for (int p = 0; p < 2; p++)
            {
                int fx = Px(from, p), fy = Py(from, p), tx = Px(to, p), ty = Py(to, p);
                if (fx == tx && fy == ty) continue;
                _sfx?.Play(SfxId.PlayerMove);
                yield return MoveRoutine(p == me, _ui.TileWorld(fx, fy), _ui.TileWorld(tx, ty),
                                         p == me ? _selfAnim : _foeAnim);
            }

            // 2) 공격 — 체력이 깎인 쪽이 있으면 시전자가 먼저 공격 자세를 잡는다
            int victim = Hp(from, 0) > Hp(to, 0) ? 0 : Hp(from, 1) > Hp(to, 1) ? 1 : -1;
            if (victim >= 0)
            {
                int attacker = 1 - victim;
                // 공격 자세는 카드를 쓴 쪽만 잡는다. 장판·구조물 피해는 상대가 때린 게
                // 아니므로, 그 쪽 말이 헛수 모션을 잡지 않게 한다.

                // 3) 피해 — 이펙트·점멸·애니메이션을 한꺼번에 터뜨린다
                bool victimIsMe = victim == me;
                var at = _ui.TileWorld(Px(to, victim), Py(to, victim));
                _sfx?.Play(SfxId.PlayerHurt);
                if (Hp(to, victim) == 0) _sfx?.Play(SfxId.PlayerDeath);
                if (!castImpact) _fx.Play(FxKind.Hit, at);   // 카드 전용 임팩트가 있으면 그걸로 갈음한다
                _fx.FlashWhite(victimIsMe ? _selfTf : _foeTf);
                Anim(victimIsMe ? _selfAnim : _foeAnim, PlayerState.DAMAGED);
                if (victimIsMe) _fx.HurtFeedback(_ui.WorldCamera(), _ui.transform);

                yield return new WaitForSeconds(damageHold);
                Anim(victimIsMe ? _selfAnim : _foeAnim, PlayerState.IDLE);
                Anim(attacker == me ? _selfAnim : _foeAnim, PlayerState.IDLE);
            }

            // 4) 그 밖의 효과 — 카드 없이 체력이 오른 경우(월드 효과)만 남는다.
            //    얼음 장판은 묶여 있는 동안 GameUI 가 유지형 연출로 계속 그린다.
            if (!cast)
                for (int p = 0; p < 2; p++)
                    if (Hp(to, p) > Hp(from, p))
                        _fx.Play(FxKind.Heal, _ui.TileWorld(Px(to, p), Py(to, p)));
            else
                Anim(caster == me ? _selfAnim : _foeAnim, PlayerState.IDLE);

            _shown = to;
            _overrideSelf = _overrideFoe = false;
            _busy = false;
        }

        IEnumerator MoveRoutine(bool isSelf, Vector3 from, Vector3 to, SPUM_Prefabs spum)
        {
            Anim(spum, PlayerState.MOVE);
            if (isSelf) { _selfPos = from; _overrideSelf = true; }
            else { _foePos = from; _overrideFoe = true; }

            float t = 0f;
            while (t < moveSeconds)
            {
                t += Time.deltaTime;
                var p = Vector3.Lerp(from, to, Mathf.Clamp01(t / moveSeconds));
                if (isSelf) _selfPos = p; else _foePos = p;
                yield return null;
            }

            if (isSelf) _selfPos = to; else _foePos = to;
            Anim(spum, PlayerState.IDLE);
        }

        static int Px(GameState s, int p) => p == 0 ? s.p0X : s.p1X;
        static int Py(GameState s, int p) => p == 0 ? s.p0Y : s.p1Y;
        static int Hp(GameState s, int p) => p == 0 ? s.p0Hp : s.p1Hp;
    }
}
