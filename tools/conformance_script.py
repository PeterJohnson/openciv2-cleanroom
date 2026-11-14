"""X02 scripted-v1 at F02's real shared-city-workspace dispatch boundary.

The C03 pipeline is an explicitly supplied child. It is not a game implementation.
"""
from conformance_values import integer, scalar, keys, logical, require, text

OPERATION = "F02.cityCalculationEntry"
PROVIDER = "C03.fullCityRecalculation"
ARGUMENTS = {"city": "city-slot-or-null", "full": "bool"}
STATES = {"F02.workspaceCity": "city-slot-or-null", "F02.happyCount": "i32", "F02.unhappyCount": "i32"}
KINDS = {"enter", "return", "provider", "effect", "snapshot", "stop"}


def segment(value):
    return value.replace("~", "~0").replace("/", "~1")


def name(value):
    require(text(value), "empty script identity")
    return value


def city(value):
    if value == {"$type": "null"}:
        return -1
    result = integer(value, "u16")
    require(result < 256, "city slot exceeds backing")
    return result


def arguments(value):
    keys(value, ARGUMENTS)
    return city(value["city"]), scalar(value["full"], "bool")


def state_values(value):
    keys(value, STATES)
    return {key: city(value[key]) if kind == "city-slot-or-null" else integer(value[key], kind)
            for key, kind in STATES.items()}


def environment(value):
    keys(value, ("protocol", "inputs", "unexpected"))
    require(value["protocol"] == "scripted-v1" and value["unexpected"] == "fail", "unknown script protocol")
    require(isinstance(value["inputs"], list) and len(value["inputs"]) <= 10000, "invalid provider input list")
    input_ids = set()
    for item in value["inputs"]:
        keys(item, ("id", "request", "arguments", "actions", "result"))
        identity = name(item["id"])
        require(identity not in input_ids, "duplicate provider input identity")
        input_ids.add(identity)
        require(item["request"] == PROVIDER, "unregistered supplied-child provider")
        keys(item["arguments"], ("city",))
        city(item["arguments"]["city"])
        require(isinstance(item["actions"], list) and len(item["actions"]) <= 10000, "invalid provider action list")
        action_ids = set()
        for action in item["actions"]:
            require(isinstance(action, dict), "action must be an object")
            kind = action.get("kind")
            fields = {"output": ("target", "value"), "deliver": ("handler", "arguments"),
                      "observe": ("label",), "failure": ("code",)}
            require(isinstance(kind, str) and kind in fields, "action is not registered for this boundary")
            keys(action, ("id", "kind", *fields[kind]))
            require(name(action["id"]) not in action_ids, "duplicate action identity")
            action_ids.add(action["id"])
            if kind == "output":
                keys(action["target"], ("stateKey",))
                target = action["target"]["stateKey"]
                require(isinstance(target, str) and target in STATES, "provider cannot write unrelated state")
                city(action["value"]) if STATES[target] == "city-slot-or-null" else integer(action["value"], "i32")
            elif kind == "deliver":
                require(action["handler"] == OPERATION, "unregistered callback")
                arguments(action["arguments"])
            elif kind == "observe":
                name(action["label"])
            else:
                name(action["code"])
        result = item["result"]
        if isinstance(result, dict) and result.get("$type") == "unavailable":
            logical(result, allow_unavailable=True)
        else:
            integer(result, "i32")


def hextext(value):
    return value.encode("utf-8").hex()


def input_words(case):
    state = state_values(case["entry"]["state"])
    words = [str(state[key]) for key in STATES]
    words += [str(case["limits"]["events"]), str(case["limits"]["providerRequests"])]
    inputs = case["environment"]["inputs"]
    words.append(str(len(inputs)))
    for item in inputs:
        words += [hextext(item["id"]), hextext(item["request"]), str(city(item["arguments"]["city"])), str(len(item["actions"]))]
        for action in item["actions"]:
            kind = action["kind"]
            words += [hextext(action["id"]), str(("output", "deliver", "observe", "failure").index(kind))]
            if kind == "output":
                target = action["target"]["stateKey"]
                value = city(action["value"]) if STATES[target] == "city-slot-or-null" else integer(action["value"], "i32")
                words += [str(list(STATES).index(target)), str(value)]
            elif kind == "deliver":
                slot, full = arguments(action["arguments"])
                words += [hextext(action["handler"]), str(slot), str(int(full))]
            else:
                words.append(hextext(action["label" if kind == "observe" else "code"]))
        if item["result"]["$type"] == "unavailable":
            words += ["1", hextext(item["result"]["reason"])]
        else:
            words += ["0", str(integer(item["result"], "i32"))]
    return words


def trace_events(trace):
    require(isinstance(trace, list), "trace must be an array")
    events = {}
    stack = []
    completed = 0
    for seq, event in enumerate(trace):
        keys(event, ("seq", "id", "kind", "call", "payload"))
        require(type(event["seq"]) is int and event["seq"] == seq, "nonconsecutive trace sequence")
        identity, call, kind = name(event["id"]), name(event["call"]), event["kind"]
        require(identity not in events and isinstance(kind, str) and kind in KINDS, "duplicate or unknown script event")
        payload = event["payload"]
        if kind == "enter":
            keys(payload, ("operation", "parent", "arguments"))
            require(payload["operation"] == OPERATION, "unknown callback operation")
            arguments(payload["arguments"])
            require(identity == call + "/enter", "entry identity mismatch")
            require(payload["parent"] == (stack[-1][1] if stack else None), "callback parent mismatch")
            stack.append(("call", call))
        elif kind == "return":
            keys(payload, ("result",))
            integer(payload["result"], "i32")
            require(identity == call + "/return", "return identity mismatch")
            require(stack and stack[-1] == ("call", call), "return violates call nesting")
            stack.pop()
        elif kind == "provider":
            require(isinstance(payload, dict), "invalid provider payload")
            require(identity == call + "/pipeline/" + str(payload.get("stage")), "provider event identity mismatch")
            if payload.get("stage") == "request":
                keys(payload, ("stage", "id", "operation", "arguments"))
                require(payload["operation"] == PROVIDER and payload["id"] == call + "/pipeline", "provider identity mismatch")
                keys(payload["arguments"], ("city",))
                city(payload["arguments"]["city"])
                require(stack and stack[-1] == ("call", call), "provider request violates nesting")
                stack.append(("provider", call))
            else:
                keys(payload, ("stage", "id", "result"))
                require(payload["stage"] == "return" and payload["id"] == call + "/pipeline", "provider return mismatch")
                integer(payload["result"], "i32")
                require(stack and stack[-1] == ("provider", call), "provider return violates nesting")
                stack.pop()
                completed += 1
        elif kind == "snapshot":
            keys(payload, ("label", "state"))
            name(payload["label"])
            state_values(payload["state"])
            if identity == "root/exit":
                require(not stack and call == "root" and payload["label"] == "exit" and seq == len(trace) - 1,
                        "invalid normal exit snapshot")
            elif identity == "root/prefix":
                require(payload["label"] == "stop-prefix" and seq == len(trace) - 2 and trace[-1]["kind"] == "stop",
                        "invalid stop-prefix snapshot")
            else:
                require(stack and stack[-1] == ("provider", call), "observation outside active provider")
        elif kind == "effect":
            keys(payload, ("name", "values"))
            require(payload["name"] in ("provider.output", "provider.failure"), "unregistered effect")
            require(stack and stack[-1] == ("provider", call), "effect outside active provider")
            values = payload["values"]
            if payload["name"] == "provider.output":
                keys(values, ("target", "value"))
                target = scalar(values["target"], "text")
                require(target in STATES, "effect targets unrelated state")
                city(values["value"]) if STATES[target] == "city-slot-or-null" else integer(values["value"], "i32")
            else:
                keys(values, ("code",))
                name(scalar(values["code"], "text"))
        else:
            keys(payload, ("category", "boundary", "reason", "completedInputs", "snapshot"))
            require(payload["category"] in ("safe-stop", "unresolved-input", "unexpected-input", "budget-exhausted"), "unknown stop category")
            name(payload["boundary"])
            name(payload["reason"])
            require(type(payload["completedInputs"]) is int and payload["completedInputs"] == completed, "completed input count mismatch")
            require(seq and payload["snapshot"] == trace[seq - 1]["id"] and trace[seq - 1]["kind"] == "snapshot", "missing stop-prefix snapshot")
            require(identity == "root/stop" and trace[seq - 1]["call"] == call, "stop invocation mismatch")
            require(seq == len(trace) - 1, "events fabricated after stop")
        events[identity] = event
    if not trace or trace[-1]["kind"] != "stop":
        require(not stack, "unfinished call/provider at normal exit")
    return events


def validate_script_order(case):
    """Validate the declared schedule, without calculating gameplay or snapshots.

    A fixture cannot bless missing/reordered actions by changing its order edges.
    Only supplied results and callback identities are inferred from the script.
    """
    trace = iter(case["expected"]["trace"])
    inputs = iter(case["environment"]["inputs"])
    stopped = False

    def take(identity, kind, call, payload=None):
        event = next(trace, None)
        require(event is not None and (event["id"], event["kind"], event["call"]) ==
                (identity, kind, call), "trace does not follow declared script order")
        if payload is not None:
            require(event["payload"] == payload, "trace differs from declared action/request")
        return event["payload"]

    def invoke(call, parent, args, depth=0):
        nonlocal stopped
        require(depth < 64, "script exceeds callback depth bound")
        take(call + "/enter", "enter", call, {"operation": OPERATION, "parent": parent, "arguments": args})
        slot, full = arguments(args)
        supplied_result = None
        if slot != -1 and full:
            identity = call + "/pipeline"
            item = next(inputs, None)
            require(item is not None and item["id"] == identity and item["arguments"] == {"city": args["city"]},
                    "provider script does not match invocation")
            take(identity + "/request", "provider", call,
                 {"stage": "request", "id": identity, "operation": PROVIDER, "arguments": item["arguments"]})
            for action in item["actions"]:
                action_id = call + "/action/" + segment(action["id"])
                kind = action["kind"]
                if kind == "deliver":
                    invoke(call + "/deliver/" + segment(action["id"]), call, action["arguments"], depth + 1)
                    if stopped:
                        return
                elif kind == "observe":
                    require(take(action_id, "snapshot", call)["label"] == action["label"], "observation label mismatch")
                else:
                    values = ({"target": {"$type": "text", "value": action["target"]["stateKey"]}, "value": action["value"]}
                              if kind == "output" else {"code": {"$type": "text", "value": action["code"]}})
                    take(action_id, "effect", call, {"name": "provider." + kind, "values": values})
            supplied_result = item["result"]
            if supplied_result["$type"] == "unavailable":
                take("root/prefix", "snapshot", call)
                stop = take("root/stop", "stop", call)
                require(stop["category"] == "unresolved-input" and stop["boundary"] == identity + "/return" and
                        stop["reason"] == supplied_result["reason"], "unresolved stop does not identify missing input")
                stopped = True
                return
            take(identity + "/return", "provider", call, {"stage": "return", "id": identity, "result": supplied_result})
        result = take(call + "/return", "return", call)["result"]
        if supplied_result is not None:
            require(result == supplied_result, "call return differs from supplied pipeline result")

    invoke("root", None, case["boundary"]["arguments"])
    if not stopped:
        require(next(inputs, None) is None, "unused mandatory provider input")
        require(take("root/exit", "snapshot", "root")["label"] == "exit", "invalid exit snapshot")
    require(next(trace, None) is None, "extra events after script end")
    require(stopped == (case["expected"]["outcome"] == "unresolved-input"), "script outcome mismatch")


def validate_expected(case):
    expected = case["expected"]
    keys(expected, ("outcome", "result", "state", "trace"))
    require(expected["outcome"] in ("return", "unresolved-input"), "unsupported scripted expected outcome")
    state_values(expected["state"])
    events = trace_events(expected["trace"])
    validate_script_order(case)
    require("root/enter" in events and events["root/enter"]["payload"] ==
            {"operation": OPERATION, "parent": None, "arguments": case["boundary"]["arguments"]}, "script entry mismatch")
    if expected["outcome"] == "return":
        integer(expected["result"], "i32")
        require("root/return" in events and events["root/return"]["payload"]["result"] == expected["result"] and
                "root/exit" in events and events["root/exit"]["payload"]["state"] == expected["state"], "script result/exit mismatch")
    else:
        require(expected["result"] == {"$type": "null"} and expected["trace"][-1]["kind"] == "stop" and
                expected["trace"][-1]["payload"]["category"] == expected["outcome"] and
                expected["trace"][-2]["payload"]["state"] == expected["state"] and "root/return" not in events,
                "script stop/result/prefix mismatch")
    return events


def consumed(actual):
    return {"providers": sum(event["kind"] == "provider" and event["payload"]["stage"] == "return" for event in actual["trace"]),
            "choices": 0, "events": len(actual["trace"])}


def compare(case, actual):
    keys(actual, ("outcome", "result", "state", "trace"))
    state_values(actual["state"])
    events = trace_events(actual["trace"])
    if actual["outcome"] == "return":
        integer(actual["result"], "i32")
        require("root/exit" in events and events["root/exit"]["payload"]["state"] == actual["state"] and
                "root/return" in events and events["root/return"]["payload"]["result"] == actual["result"],
                "actual result/exit mismatch")
    else:
        require(actual["result"] == {"$type": "null"} and "root/stop" in events and
                events["root/stop"]["payload"]["category"] == actual["outcome"] and
                "root/prefix" in events and events["root/prefix"]["payload"]["state"] == actual["state"],
                "actual stop/result/prefix mismatch")
    for key in ("outcome", "result", "state"):
        if actual[key] != case["expected"][key]:
            return {"surface": key, "expected": case["expected"][key], "actual": actual[key]}
    expected = {event["id"]: event for event in case["expected"]["trace"]}
    if events.keys() != expected.keys():
        return {"surface": "trace.identities", "expected": list(expected), "actual": list(events)}
    for identity, event in expected.items():
        for key in ("kind", "call", "payload"):
            if events[identity][key] != event[key]:
                return {"surface": f"trace.{identity}.{key}", "expected": event[key], "actual": events[identity][key]}
    for first, second in case["comparison"]["order"]["before"]:
        if events[first]["seq"] >= events[second]["seq"]:
            return {"surface": "trace.order", "edge": [first, second]}
    return None
